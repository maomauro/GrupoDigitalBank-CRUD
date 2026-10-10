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
$fallos = 0

function Check($nombre, $ok, $detalle = '') {
    if (-not $ok) { $script:fallos++ }
    Write-Host ("{0,-6} {1} {2}" -f $(if ($ok) { 'OK' } else { 'FALLA' }), $nombre, $detalle) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
}

function Sql($consulta) {
    (docker exec $bd psql -U postgres -d pruebas -At -c $consulta 2>&1) -join "`n"
}

# Ejecuta el migrador montando $tmp como /migraciones; devuelve codigo y salida
function Migrar() {
    $salida = docker run --rm --network $red -v "${tmp}:/migraciones:ro" `
        -e PGHOST=$bd -e PGUSER=postgres -e PGPASSWORD=clave -e PGDATABASE=pruebas `
        $imagen sh /migraciones/migrate.sh 2>&1
    [pscustomobject]@{ Codigo = $LASTEXITCODE; Salida = ($salida -join "`n") }
}

function ReiniciarScripts() {
    Remove-Item "$tmp\*" -Force -ErrorAction SilentlyContinue
    Copy-Item "$raiz\db\migrate.sh" "$tmp\migrate.sh"
    Copy-Item "$raiz\db\001_esquema_usuario.sql" "$tmp\001_esquema_usuario.sql"
}

function EscribirLf($ruta, $texto) { [IO.File]::WriteAllText($ruta, ($texto -replace "`r`n", "`n"), (New-Object Text.UTF8Encoding($false))) }

try {
    New-Item -ItemType Directory -Force $tmp | Out-Null
    docker network create $red | Out-Null
    docker run -d --name $bd --network $red -e POSTGRES_PASSWORD=clave -e POSTGRES_DB=pruebas $imagen | Out-Null
    for ($i = 0; $i -lt 40; $i++) { docker exec $bd pg_isready -U postgres -d pruebas 2>$null | Out-Null; if ($LASTEXITCODE -eq 0) { Start-Sleep 1; break }; Start-Sleep 1 }

    Write-Host "`n== 1. Base vacia: aplica el 001 y lo registra ==" -ForegroundColor Cyan
    ReiniciarScripts
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0) $r.Salida.Split("`n")[-1]
    Check 'registra 1 migracion' ((Sql 'select count(*) from schema_migrations') -eq '1')
    Check 'la funcion fn_usuario_crud existe' ((Sql "select count(*) from pg_proc where proname='fn_usuario_crud'") -eq '1')

    Write-Host "`n== 2. Segunda ejecucion: no repite nada ==" -ForegroundColor Cyan
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0)
    Check 'dice que ya estaba aplicado' ($r.Salida -match '001_esquema_usuario.sql ya aplicado')
    Check 'sigue habiendo 1 migracion' ((Sql 'select count(*) from schema_migrations') -eq '1')

    Write-Host "`n== 3. Script nuevo valido: se aplica solo ese ==" -ForegroundColor Cyan
    EscribirLf "$tmp\002_prueba_ok.sql" "CREATE TABLE IF NOT EXISTS prueba_ok (id int);`n"
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0)
    Check 'ahora hay 2 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '2')
    Check 'la tabla nueva existe' ((Sql "select to_regclass('prueba_ok') is not null") -eq 't')

    Write-Host "`n== 4. Script con error: se revierte todo y no se registra ==" -ForegroundColor Cyan
    EscribirLf "$tmp\003_prueba_falla.sql" "CREATE TABLE prueba_parcial (id int);`nSELECT esta_funcion_no_existe();`n"
    $r = Migrar
    Check 'termina con error (codigo distinto de 0)' ($r.Codigo -ne 0) "codigo=$($r.Codigo)"
    Check 'no se creo la tabla parcial (transaccion revertida)' ((Sql "select to_regclass('prueba_parcial') is null") -eq 't')
    Check 'sigue habiendo 2 migraciones' ((Sql 'select count(*) from schema_migrations') -eq '2')
    Remove-Item "$tmp\003_prueba_falla.sql"

    Write-Host "`n== 5. Script ya aplicado modificado: se aborta ==" -ForegroundColor Cyan
    Add-Content "$tmp\002_prueba_ok.sql" "-- editado despues de aplicarse"
    $r = Migrar
    Check 'termina con error (codigo distinto de 0)' ($r.Codigo -ne 0) "codigo=$($r.Codigo)"
    Check 'el mensaje explica que no se editan' ($r.Salida -match 'fue modificado')

    Write-Host "`n== 6. Base existente sin registro (la de desarrollo): conserva los datos ==" -ForegroundColor Cyan
    docker exec $bd psql -U postgres -d pruebas -q -c "DROP TABLE schema_migrations" | Out-Null
    $null = Sql "insert into usuario (nombre, fecha_nacimiento, sexo) values ('Dato previo', '1990-01-01', 'M')"
    ReiniciarScripts
    $r = Migrar
    Check 'termina con exito' ($r.Codigo -eq 0)
    Check 'el dato previo sigue ahi' ((Sql "select count(*) from usuario where nombre='Dato previo'") -eq '1')
    Check 'registra el 001' ((Sql 'select count(*) from schema_migrations') -eq '1')
}
finally {
    docker rm -f $bd 2>$null | Out-Null
    docker network rm $red 2>$null | Out-Null
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
if ($fallos -eq 0) { Write-Host 'Migrador: todas las pruebas pasaron.' -ForegroundColor Green; exit 0 }
Write-Host "Migrador: $fallos prueba(s) fallaron." -ForegroundColor Red; exit 1
