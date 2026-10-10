# Prueba el migrador (db/migrate.sh) en una base PostgreSQL desechable, sin tocar la base de desarrollo.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\probar-migrador.ps1
# Requiere Docker. Crea una red y un contenedor temporales y los elimina al terminar.

# 'Continue': en PowerShell 5.1 la salida por stderr de un comando nativo (docker/psql) no debe abortar el script.
# Los resultados se comprueban de forma explicita con $LASTEXITCODE y las consultas.
$ErrorActionPreference = 'Continue'
$raiz = Split-Path -Parent $PSScriptRoot
$sufijo = [guid]::NewGuid().ToString('N').Substring(0, 8)
$red = "gdbmig-red-$sufijo"
$bd = "gdbmig-bd-$sufijo"
$tmp = Join-Path ([IO.Path]::GetTempPath()) "gdbmig-$sufijo"
$imagen = 'postgres:17-alpine'
$usuarioApp = 'usuarios_app'
$claveApp = 'clave-app-solo-pruebas'
$fallos = 0

function Check($nombre, $ok, $detalle = '') {
    if (-not $ok) { $script:fallos++ }
    Write-Host ("{0,-6} {1} {2}" -f $(if ($ok) { 'OK' } else { 'FALLA' }), $nombre, $detalle) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
}

function Sql($consulta) {
    (docker exec $bd psql -U postgres -d pruebas -At -c $consulta 2>&1) -join "`n"
}

# Ejecuta el migrador montando $tmp como /migraciones; devuelve codigo y salida.
# $entorno permite sobreescribir las variables (para probar la validacion de entrada).
function Migrar($entorno = @{}) {
    $vars = @{ PGHOST = $bd; PGUSER = 'postgres'; PGPASSWORD = 'clave'; PGDATABASE = 'pruebas'
               APP_DB_USER = $usuarioApp; APP_DB_PASSWORD = $claveApp }
    foreach ($k in $entorno.Keys) { $vars[$k] = $entorno[$k] }
    $argumentos = @('run', '--rm', '--network', $red, '-v', "${tmp}:/migraciones:ro")
    foreach ($k in $vars.Keys) { if ($null -ne $vars[$k]) { $argumentos += @('-e', "$k=$($vars[$k])") } }
    $argumentos += @($imagen, 'sh', '/migraciones/migrate.sh')
    $salida = & docker @argumentos 2>&1
    [pscustomobject]@{ Codigo = $LASTEXITCODE; Salida = ($salida -join "`n") }
}

function ReiniciarScripts() {
    Remove-Item "$tmp\*" -Force -ErrorAction SilentlyContinue
    Copy-Item "$raiz\db\migrate.sh" "$tmp\migrate.sh"
    Copy-Item "$raiz\db\001_esquema_usuario.sql" "$tmp\001_esquema_usuario.sql"
    Copy-Item "$raiz\db\002_rol_aplicacion.sql" "$tmp\002_rol_aplicacion.sql"
    Copy-Item "$raiz\db\003_fecha_nacimiento_hora_de_colombia.sql" "$tmp\003_fecha_nacimiento_hora_de_colombia.sql"
    Copy-Item "$raiz\db\004_nombre_no_vacio.sql" "$tmp\004_nombre_no_vacio.sql"
    Copy-Item "$raiz\db\005_paginacion_usuarios.sql" "$tmp\005_paginacion_usuarios.sql"
}

function EscribirLf($ruta, $texto) { [IO.File]::WriteAllText($ruta, ($texto -replace "`r`n", "`n"), (New-Object Text.UTF8Encoding($false))) }

try {
    New-Item -ItemType Directory -Force $tmp | Out-Null
    docker network create $red | Out-Null
    docker run -d --name $bd --network $red -e POSTGRES_PASSWORD=clave -e POSTGRES_DB=pruebas $imagen | Out-Null
    for ($i = 0; $i -lt 40; $i++) { docker exec $bd pg_isready -U postgres -d pruebas 2>$null | Out-Null; if ($LASTEXITCODE -eq 0) { Start-Sleep 1; break }; Start-Sleep 1 }

    Write-Host "`n== 1. Base vacia: aplica 001 a 005 y los registra ==" -ForegroundColor Cyan
    ReiniciarScripts
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0) $r.Salida.Split("`n")[-1]
    Check 'registra 5 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '5')
    Check 'la funcion fn_usuario_crud existe' ((Sql "select count(*) from pg_proc where proname='fn_usuario_crud'") -eq '1')
    Check 'la funcion fn_usuario_pagina existe' ((Sql "select count(*) from pg_proc where proname='fn_usuario_pagina'") -eq '1')
    Check 'el rol de aplicacion existe, sin superusuario' ((Sql "select rolsuper from pg_roles where rolname='$usuarioApp'") -eq 'f')

    Write-Host "`n== 2. Segunda ejecucion: no repite nada ==" -ForegroundColor Cyan
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0)
    Check 'dice que ya estaba aplicado' ($r.Salida -match '001_esquema_usuario.sql ya aplicado' -and $r.Salida -match '005_paginacion_usuarios.sql ya aplicado')
    Check 'siguen siendo 5 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '5')

    Write-Host "`n== 3. Script nuevo valido: se aplica solo ese ==" -ForegroundColor Cyan
    EscribirLf "$tmp\006_prueba_ok.sql" "CREATE TABLE IF NOT EXISTS prueba_ok (id int);`n"
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0)
    Check 'ahora hay 6 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '6')
    Check 'la tabla nueva existe' ((Sql "select to_regclass('prueba_ok') is not null") -eq 't')

    Write-Host "`n== 4. Script con error: se revierte todo y no se registra ==" -ForegroundColor Cyan
    EscribirLf "$tmp\007_prueba_falla.sql" "CREATE TABLE prueba_parcial (id int);`nSELECT esta_funcion_no_existe();`n"
    $r = Migrar
    Check 'termina con error (codigo distinto de 0)' ($r.Codigo -ne 0) "codigo=$($r.Codigo)"
    Check 'no se creo la tabla parcial (transaccion revertida)' ((Sql "select to_regclass('prueba_parcial') is null") -eq 't')
    Check 'siguen siendo 6 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '6')
    Remove-Item "$tmp\007_prueba_falla.sql"

    Write-Host "`n== 5. Script ya aplicado modificado: se aborta ==" -ForegroundColor Cyan
    Add-Content "$tmp\006_prueba_ok.sql" "-- editado despues de aplicarse"
    $r = Migrar
    Check 'termina con error (codigo distinto de 0)' ($r.Codigo -ne 0) "codigo=$($r.Codigo)"
    Check 'el mensaje explica que no se editan' ($r.Salida -match 'fue modificado')

    Write-Host "`n== 6. Base existente sin registro (la de desarrollo): conserva los datos ==" -ForegroundColor Cyan
    docker exec $bd psql -U postgres -d pruebas -q -c "DROP TABLE schema_migrations" | Out-Null
    $null = Sql "insert into usuario (nombre, fecha_nacimiento, sexo) values ('Dato previo', '1990-01-01', 'M')"
    ReiniciarScripts
    $r = Migrar
    Check 'termina con exito (los scripts son idempotentes)' ($r.Codigo -eq 0) $r.Salida.Split("`n")[-1]
    Check 'el dato previo sigue ahi' ((Sql "select count(*) from usuario where nombre='Dato previo'") -eq '1')
    Check 'registra 001 a 005' ((Sql 'select count(*) from schema_migrations') -eq '5')

    Write-Host "`n== 7. Rol de aplicacion: minimo privilegio y rotacion de contrasena ==" -ForegroundColor Cyan
    function ConectaApp($clave) {
        $null = docker run --rm --network $red -e PGPASSWORD=$clave $imagen psql -h $bd -U $usuarioApp -d pruebas -At -c "select fn_usuario_crud('R')" 2>&1
        $LASTEXITCODE -eq 0
    }
    Check 'el rol de aplicacion se conecta con su contrasena' (ConectaApp $claveApp)
    $directo = docker run --rm --network $red -e PGPASSWORD=$claveApp $imagen psql -h $bd -U $usuarioApp -d pruebas -At -c "select * from usuario" 2>&1
    Check 'no puede leer la tabla directamente' ($LASTEXITCODE -ne 0 -and ($directo -join ' ') -match 'permission denied')

    $r = Migrar @{ APP_DB_PASSWORD = 'otra-clave-rotada' }   # los scripts ya estan aplicados; solo cambia el entorno
    Check 'migrar con otra APP_DB_PASSWORD termina con exito' ($r.Codigo -eq 0)
    Check 'la contrasena nueva funciona' (ConectaApp 'otra-clave-rotada')
    Check 'la contrasena anterior deja de funcionar' (-not (ConectaApp $claveApp))
    $r = Migrar                                               # vuelve a la original
    Check 'se puede volver a la contrasena original' ((ConectaApp $claveApp) -and -not (ConectaApp 'otra-clave-rotada'))

    Write-Host "`n== 8. Validacion de entrada del migrador ==" -ForegroundColor Cyan
    $r = Migrar @{ APP_DB_PASSWORD = '' }
    Check 'falla si falta APP_DB_PASSWORD' ($r.Codigo -ne 0 -and $r.Salida -match 'APP_DB_PASSWORD es obligatoria')
    $r = Migrar @{ APP_DB_USER = 'postgres' }
    Check 'falla si la app usaria el mismo usuario que migra' ($r.Codigo -ne 0 -and $r.Salida -match 'no puede ser el mismo usuario')

    Write-Host "`n== 9. 004 con nombres vacios ya guardados: falla con mensaje claro y no cambia nada ==" -ForegroundColor Cyan
    # Se reproduce una base anterior al 004: sin la restriccion, sin su registro y con dos filas invalidas
    $null = Sql "alter table usuario drop constraint ck_usuario_nombre"
    $null = Sql "delete from schema_migrations where version like '004%'"
    $null = Sql "insert into usuario (nombre, fecha_nacimiento, sexo) values ('   ', '1990-01-01', 'M'), ('', '1991-02-02', 'F')"
    $r = Migrar
    Check 'termina con error (codigo distinto de 0)' ($r.Codigo -ne 0) "codigo=$($r.Codigo)"
    Check 'el mensaje cuenta las filas y da sus ids' ($r.Salida -match '2 usuario\(s\) tienen el nombre vacio' -and $r.Salida -match 'ids: \d+, \d+')
    Check 'no se creo la restriccion' ((Sql "select count(*) from pg_constraint where conname='ck_usuario_nombre'") -eq '0')
    Check 'no registro el script' ((Sql "select count(*) from schema_migrations where version like '004%'") -eq '0')
    Check 'no borro ni cambio las filas' ((Sql "select count(*) from usuario where nombre !~ '\S'") -eq '2')
    $null = Sql "delete from usuario where nombre !~ '\S'"
    $r = Migrar
    Check 'tras corregir los datos, migra con exito' ($r.Codigo -eq 0) $r.Salida.Split("`n")[-1]
    Check 'la restriccion existe y el 004 queda registrado' ((Sql "select count(*) from pg_constraint where conname='ck_usuario_nombre'") -eq '1' -and (Sql "select count(*) from schema_migrations where version like '004%'") -eq '1')
}
finally {
    docker rm -f $bd 2>$null | Out-Null
    docker network rm $red 2>$null | Out-Null
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
if ($fallos -eq 0) { Write-Host 'Migrador: todas las pruebas pasaron.' -ForegroundColor Green; exit 0 }
Write-Host "Migrador: $fallos prueba(s) fallaron." -ForegroundColor Red; exit 1
