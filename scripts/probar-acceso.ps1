# Prueba el control de acceso del proxy (Basic Auth de Caddy, hallazgo H2) contra un stack en marcha.
# Verifica que todo exige credenciales salvo /health y las rutas publicas del inicio (/, /css, /js, /salir; ADR 0016), que ninguna variante de ruta evade la proteccion,
# que HTTP redirige a HTTPS y que la aplicacion funciona de punta a punta a traves del proxy.
#
# Requisitos: el stack levantado con el perfil del proxy y AUTH_USUARIO/AUTH_HASH en .env, p. ej.:
#   docker compose -f docker-compose.yml -f docker-compose.dev.yml --profile con-proxy up -d --build
# Uso:
#   powershell -ExecutionPolicy Bypass -File scripts\probar-acceso.ps1 -Usuario admin     (pide la clave)
#   powershell -ExecutionPolicy Bypass -File scripts\probar-acceso.ps1 -Usuario admin -Clave $secure -Base https://localhost
# No borra datos previos: el flujo de punta a punta solo elimina lo que el mismo crea.

param(
    [string]$Base = 'https://localhost',
    [string]$Usuario = 'admin',
    [securestring]$Clave,
    # max-age esperado en Strict-Transport-Security. Por omision: 0 en localhost (el compose de desarrollo
    # lo desactiva) y 31536000 (1 ano) en cualquier otro dominio.
    [Nullable[int]]$HstsEsperado = $null
)

$ErrorActionPreference = 'Continue'
$Base = $Base.TrimEnd('/')
if (-not $Clave) { $Clave = Read-Host "Clave de '$Usuario'" -AsSecureString }
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Clave)
$claveTexto = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)

# Certificado local de Caddy (CA interna) no es de confianza en este equipo: solo para pruebas locales
if ($Base -like 'https://localhost*' -or $Base -like 'https://127.*') {
    [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    $curlInseguro = '-k'
} else { $curlInseguro = '' }

$fallos = 0
function Check($nombre, $ok, $detalle = '') {
    if (-not $ok) { $script:fallos++ }
    Write-Host ("{0,-6} {1,-58} {2}" -f $(if ($ok) { 'OK' } else { 'FALLA' }), $nombre, $detalle) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
}

function Http([string[]]$esperados, [string[]]$argumentos) {
    $c = & curl.exe -s $curlInseguro -o NUL -w "%{http_code}" --max-time 20 @argumentos
    [pscustomobject]@{ Codigo = $c; Ok = ($esperados -contains $c) }
}
function Prueba($nombre, [string[]]$esperados, [string[]]$argumentos) {
    $r = Http $esperados $argumentos
    Check $nombre $r.Ok "-> HTTP $($r.Codigo) (esperado $($esperados -join ' o '))"
}
# Encabezado armado a mano: curl -u tropieza con algunos caracteres especiales
$basic = [Convert]::ToBase64String((New-Object Text.UTF8Encoding($false)).GetBytes("${Usuario}:$claveTexto"))
$auth = @('-H', "Authorization: Basic $basic")

Write-Host "`n== Credenciales ==" -ForegroundColor Cyan
Prueba 'GET /Usuarios/Consultar sin credenciales' @('401') @("$Base/Usuarios/Consultar")
Prueba 'GET /Usuarios/Consultar con clave incorrecta' @('401') @('-H', ('Authorization: Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${Usuario}:clave-equivocada-xyz"))), "$Base/Usuarios/Consultar")
Prueba 'GET /Usuarios/Consultar con usuario incorrecto' @('401') @('-H', ('Authorization: Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("otro:$claveTexto"))), "$Base/Usuarios/Consultar")
Prueba 'GET /Usuarios/Consultar con credenciales correctas' @('200') ($auth + "$Base/Usuarios/Consultar")
Prueba 'GET /Usuarios/Registrar con credenciales' @('200') ($auth + "$Base/Usuarios/Registrar")

Write-Host "`n== Todo lo demas exige credenciales ==" -ForegroundColor Cyan
foreach ($ruta in '/Usuarios/Consultar', '/Usuarios/Registrar', '/Usuarios/Editar/1', '/Inicio', '/Inicio/Index', '/Inicio/Salir', '/inicio', '/acerca', '/Inicio/Panel', '/Inicio/Acerca', '/Usuarios') { Prueba "GET $ruta" @('401') @("$Base$ruta") }
foreach ($m in 'POST', 'PUT', 'DELETE', 'PATCH', 'OPTIONS') { Prueba "$m /Usuarios/Eliminar/1" @('401') @('-X', $m, "$Base/Usuarios/Eliminar/1") }

Write-Host "`n== Rutas publicas del inicio (ADR 0016): solo ellas, y sin datos ==" -ForegroundColor Cyan
Prueba 'GET / (inicio) sin credenciales' @('200') @("$Base/")
Prueba 'GET /css/site.css sin credenciales' @('200') @("$Base/css/site.css")
Prueba 'GET /js/site.js sin credenciales' @('200') @("$Base/js/site.js")
$inicio = (& curl.exe -s $curlInseguro --max-time 20 "$Base/") -join "`n"
Check 'El inicio explica la solucion y no muestra datos de usuarios' (($inicio -match 'Arquitectura') -and ($inicio -notmatch '/Usuarios/Editar/')) 'sin enlaces de edicion de la grilla'
$cuerpoQuery = (& curl.exe -s $curlInseguro --max-time 20 "$Base/?x=/Usuarios/Consultar") -join "`n"
Check 'GET /?x=/Usuarios/Consultar sigue siendo solo el inicio' (($cuerpoQuery -match 'Arquitectura') -and ($cuerpoQuery -notmatch '/Usuarios/Editar/'))
$rs = (& curl.exe -s $curlInseguro -D - -o NUL --max-time 20 "$Base/salir") -join "`n"
Check 'GET /salir: 401 con el desafio Basic del mismo realm y sin cache' (($rs -match '(?m)^HTTP/\S+ 401') -and ($rs -match 'WWW-Authenticate: Basic realm="restricted"') -and ($rs -match '(?im)^Cache-Control: no-store'))
$rsc = (& curl.exe -s $curlInseguro --max-time 20 "$Base/salir") -join "`n"
Check 'La pagina de /salir dice que la sesion se cerro' ($rsc -match 'Sesi.{1,2}n cerrada')
# Los comodines css/ y js/ NO deben dejar pasar rutas protegidas ni variantes de /salir
foreach ($ruta in '/css/../Usuarios/Consultar', '/js/../Usuarios/Consultar', '/css/%2e%2e/Usuarios/Consultar', '/css/..%2fUsuarios/Consultar', '/js/%2e%2e%2fUsuarios/Consultar', '/salir/', '/salir/x', '/salir/../Usuarios/Consultar', '/css/../Inicio/Salir') {
    Prueba "GET $ruta (evasion)" @('401', '404') @('--path-as-is', "$Base$ruta")
}
$bypass = (& curl.exe -s $curlInseguro --max-time 20 --path-as-is "$Base/css/../Usuarios/Consultar") -join "`n"
Check 'Ninguna evasion devuelve la grilla' (($bypass -notmatch '/Usuarios/Editar/') -and ($bypass -notmatch 'Gesti.n de usuarios'))
Prueba 'GET //Usuarios/Consultar (doble barra)' @('401') @("$Base//Usuarios/Consultar")
Prueba 'POST /salir' @('401', '404', '405') @('-X', 'POST', "$Base/salir")

Prueba 'GET /inicio con credenciales (inicio con sesion y grilla)' @('200') ($auth + "$Base/inicio")
Prueba 'GET /acerca con credenciales' @('200') ($auth + "$Base/acerca")

Write-Host "`n== /health abierto, pero SOLO /health ==" -ForegroundColor Cyan
Prueba 'GET /health sin credenciales' @('200') @("$Base/health")
Prueba 'GET /HEALTH (mayusculas)' @('200') @("$Base/HEALTH")
Prueba 'GET /health?x=/Usuarios/Consultar' @('200') @("$Base/health?x=/Usuarios/Consultar")
foreach ($ruta in '/health/', '/health/x', '/healthz', '/health.json', '/healthcheck', '/health;x') { Prueba "GET $ruta" @('401') @("$Base$ruta") }
Prueba 'GET /health/../Usuarios/Consultar' @('401') @('--path-as-is', "$Base/health/../Usuarios/Consultar")
Prueba 'GET //Usuarios/Consultar (doble barra)' @('401') @("$Base//Usuarios/Consultar")
# Caddy trata //health como /health y la app no tiene esa ruta: 404, sin exponer nada protegido
Prueba 'GET //health (doble barra)' @('404', '401') @("$Base//health")
$cuerpo = (& curl.exe -s $curlInseguro --max-time 20 "$Base/health?x=/Usuarios/Consultar")
Check '/health devuelve solo "ok" (no contenido protegido)' ($cuerpo -eq 'ok') "cuerpo=[$cuerpo]"

Write-Host "`n== Cabeceras ==" -ForegroundColor Cyan
$cab = (& curl.exe -s $curlInseguro -D - -o NUL --max-time 20 "$Base/Usuarios/Consultar") -join "`n"
Check 'responde 401 con desafio Basic' ($cab -match '401' -and $cab -match 'WWW-Authenticate: Basic')
if ($Base -like 'https://*') {
    $http = $Base -replace '^https', 'http'
    $r = (& curl.exe -s -o NUL -w "%{http_code} %{redirect_url}" --max-time 20 "$http/")
    Check 'HTTP redirige a HTTPS antes de pedir credenciales' ($r -match '^30[1278] https://') "-> $r"
}

Write-Host "`n== Cabeceras de seguridad (en todo tipo de respuesta) ==" -ForegroundColor Cyan
if ($null -eq $HstsEsperado) { $HstsEsperado = $(if ($Base -like 'https://localhost*' -or $Base -like 'https://127.*') { 0 } else { 31536000 }) }

function Cabeceras([string[]]$argumentos) {
    $crudo = (& curl.exe -s $curlInseguro -D - -o NUL --max-time 20 @argumentos) -join "`n"
    $h = @{}
    foreach ($linea in ($crudo -split "`r?`n")) { if ($linea -match '^([A-Za-z0-9-]+):\s*(.*)$') { $h[$Matches[1].ToLower()] = $Matches[2].Trim() } }
    $h
}
$respuestas = [ordered]@{
    '401 sin credenciales (GET protegido)' = @("$Base/Usuarios/Consultar")
    '200 inicio publico (GET /)'     = @("$Base/")
    '401 /salir (cierre de sesion)'  = @("$Base/salir")
    '401 sin credenciales (POST)'    = @('-X', 'POST', "$Base/Usuarios/Eliminar/1")
    '200 /health (abierto)'          = @("$Base/health")
    '200 pagina con credenciales'    = ($auth + "$Base/Usuarios/Registrar")
    '200 estatico CSS (publico)'     = @("$Base/css/site.css")
    '200 estatico JS'                = ($auth + "$Base/js/site.js")
    '404 con credenciales'           = ($auth + "$Base/ruta-que-no-existe")
}
foreach ($nombre in $respuestas.Keys) {
    $h = Cabeceras $respuestas[$nombre]
    $csp = $h['content-security-policy']
    $problemas = @()
    if ($h['x-content-type-options'] -ne 'nosniff') { $problemas += 'X-Content-Type-Options' }
    if ($h['x-frame-options'] -ne 'DENY') { $problemas += 'X-Frame-Options' }
    if ($h['referrer-policy'] -ne 'no-referrer') { $problemas += 'Referrer-Policy' }
    if (-not $h['permissions-policy']) { $problemas += 'Permissions-Policy' }
    if ($h['cross-origin-opener-policy'] -ne 'same-origin') { $problemas += 'COOP' }
    if ($h['cross-origin-resource-policy'] -ne 'same-origin') { $problemas += 'CORP' }
    foreach ($d in "default-src 'self'", "script-src 'self'", "style-src 'self'", "object-src 'none'", "base-uri 'self'", "form-action 'self'", "frame-ancestors 'none'") { if (-not $csp -or -not $csp.Contains($d)) { $problemas += "CSP:$d" } }
    if ($csp -and ($csp -match 'unsafe-inline|unsafe-eval|\*|data:|http:')) { $problemas += 'CSP demasiado permisiva' }
    if ($Base -like 'https://*' -and $h['strict-transport-security'] -ne "max-age=$HstsEsperado") { $problemas += "HSTS=[$($h['strict-transport-security'])] esperado max-age=$HstsEsperado" }
    # Server lo pone el proxy de DELANTE (nginx compartido o Cloudflare), no Caddy: se admite el nombre a secas, no una version
    if ($h.ContainsKey('server') -and $h['server'] -match '[0-9]|caddy|kestrel') { $problemas += "Server=[$($h['server'])]" }
    if ($h.ContainsKey('x-powered-by')) { $problemas += 'X-Powered-By' }
    Check $nombre ($problemas.Count -eq 0) $(if ($problemas.Count) { '-> ' + ($problemas -join '; ') } else { '' })
}

Write-Host "`n== Cadena de proxies (nginx -> Caddy -> aplicacion, ADR 0011) ==" -ForegroundColor Cyan
$crudo = (& curl.exe -s $curlInseguro -D - -o NUL --max-time 20 @auth "$Base/Usuarios/Registrar") -join "`n"
$hsts = @([regex]::Matches($crudo, '(?im)^strict-transport-security:'))
Check 'Strict-Transport-Security aparece UNA sola vez (no la duplican nginx y Caddy)' ($hsts.Count -eq 1) "veces=$($hsts.Count)"
if ($Base -like 'https://*') {
    # La cookie antiforgery solo lleva "secure" si la aplicacion cree que la peticion es HTTPS, es decir, si el
    # X-Forwarded-Proto del nginx atraviesa Caddy intacto (trusted_proxies); sin eso Caddy lo reescribiria a "http"
    Check 'La aplicacion ve HTTPS: la cookie antiforgery sale con Secure' ($crudo -match '(?im)^set-cookie:[^\r\n]*antiforgery[^\r\n]*;\s*secure') 'X-Forwarded-Proto llega a la aplicacion'
}

Write-Host "`n== Aplicacion de punta a punta a traves del proxy ==" -ForegroundColor Cyan
$cookies = Join-Path ([IO.Path]::GetTempPath()) "gdb-cookies-$([guid]::NewGuid().ToString('N')).txt"
function Pagina($ruta) { & curl.exe -s $curlInseguro -b $cookies -c $cookies @auth --max-time 20 "$Base$ruta" }
function Token($html) { [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value }
function Ids($html) { [regex]::Matches($html, '/Usuarios/Editar/(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object { [int]$_ } -Unique }
function Enviar($ruta, [string[]]$campos) {
    $t = Token (Pagina '/Usuarios/Registrar')
    $datos = @(); foreach ($c in $campos) { $datos += @('--data-urlencode', $c) }
    & curl.exe -s $curlInseguro -b $cookies -c $cookies @auth -L --max-time 20 @datos --data-urlencode "__RequestVerificationToken=$t" "$Base$ruta"
}
try {
    $antes = @(Ids (Pagina '/Usuarios/Consultar'))
    $r = Enviar '/Usuarios/Registrar' @('Nombre=Prueba Proxy', 'FechaNacimiento=1990-05-20', 'Sexo=F')
    Check 'Registrar un usuario (antiforgery + cookies via proxy)' ($r -match 'Prueba Proxy' -and $r -match 'registrado correctamente')
    $despues = @(Ids (Pagina '/Usuarios/Consultar'))
    $nuevo = $despues | Where-Object { $antes -notcontains $_ } | Select-Object -First 1
    Check 'Aparece en la grilla con id nuevo' ($null -ne $nuevo) "id=$nuevo"
    $r = Enviar '/Usuarios/Editar' @("Id=$nuevo", 'Nombre=Prueba Proxy Modificada', 'FechaNacimiento=1991-06-21', 'Sexo=F')
    Check 'Modificar' ($r -match 'Prueba Proxy Modificada' -and $r -match 'modificado correctamente')
    $r = Enviar '/Usuarios/Registrar' @('Nombre=', 'FechaNacimiento=1990-05-20', 'Sexo=F')
    Check 'Validacion: nombre vacio rechazado' ($r -match 'El nombre es obligatorio')
    $null = Enviar '/Usuarios/Eliminar' @("id=$nuevo")
    $final = @(Ids (Pagina '/Usuarios/Consultar'))
    Check 'Eliminar: quedan exactamente los usuarios previos' (($final -join ',') -eq ($antes -join ',')) "previos=[$($antes -join ',')] ahora=[$($final -join ',')]"
}
finally {
    Remove-Item $cookies -ErrorAction SilentlyContinue
    $claveTexto = $null
}

Write-Host ''
if ($fallos -eq 0) { Write-Host 'Acceso: todas las pruebas pasaron.' -ForegroundColor Green; exit 0 }
Write-Host "Acceso: $fallos prueba(s) fallaron." -ForegroundColor Red; exit 1
