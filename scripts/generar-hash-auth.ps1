# Genera el hash bcrypt de la clave de acceso a la web (Basic Auth de Caddy, hallazgo H2).
# La clave se pide sin mostrarse y se pasa a Caddy por entrada estandar (no queda en la linea de comandos
# ni en el historial); en el .env solo se guarda el HASH.
#
# Uso:
#   powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1                       # muestra las lineas
#   powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1 -ActualizarEnv        # las escribe en .env
#   powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1 -Usuario ana -ActualizarEnv
# Requiere Docker (usa la imagen caddy:2-alpine).

param(
    [string]$Usuario = 'admin',
    [securestring]$Clave,
    [switch]$ActualizarEnv,
    [string]$Env = (Join-Path (Split-Path -Parent $PSScriptRoot) '.env')
)

$ErrorActionPreference = 'Stop'

function ComoTexto([securestring]$s) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

if ($Usuario -notmatch '^[A-Za-z0-9._-]+$') { throw "Usuario no valido: use solo letras, numeros, punto, guion y guion bajo." }

if (-not $Clave) {
    $a = Read-Host "Clave para '$Usuario' (minimo 12 caracteres)" -AsSecureString
    $b = Read-Host 'Repita la clave' -AsSecureString
    if ((ComoTexto $a) -ne (ComoTexto $b)) { throw 'Las claves no coinciden.' }
    $Clave = $a
}
$texto = ComoTexto $Clave
if ($texto.Length -lt 12) { throw 'La clave debe tener al menos 12 caracteres.' }

# bcrypt de coste 14 (por defecto de Caddy): tarda un par de segundos a proposito, frena la fuerza bruta.
# IMPORTANTE: la clave NO se envia por la tuberia de PowerShell 5.1 (agrega un BOM UTF-8 y CRLF y el hash
# saldria de otra cadena) ni como argumento (quedaria visible en la lista de procesos). Viaja en base64 por una
# variable de entorno del proceso (asi no hay comillas ni caracteres especiales que PowerShell 5.1 pueda alterar)
# y la tuberia se ejecuta DENTRO del contenedor: decodifica, agrega UN salto de linea (Caddy descarta el ultimo
# byte leido) y calcula el hash sobre los bytes exactos de la clave.
$env:CLAVE_AUTH_WEB_B64 = [Convert]::ToBase64String((New-Object Text.UTF8Encoding($false)).GetBytes($texto))
try {
    $salida = docker run --rm -e CLAVE_AUTH_WEB_B64 caddy:2-alpine sh -c 'echo $CLAVE_AUTH_WEB_B64 | base64 -d | { cat; echo; } | caddy hash-password' 2>$null
    $codigo = $LASTEXITCODE   # se lee ANTES de filtrar: cortar la tuberia dejaria un codigo distinto de cero
}
finally {
    Remove-Item Env:CLAVE_AUTH_WEB_B64 -ErrorAction SilentlyContinue
}
$texto = $null
$hash = @($salida) | Where-Object { $_ } | Select-Object -First 1
if ($codigo -ne 0 -or -not $hash -or $hash -notmatch '^\$2[aby]\$\d\d\$') { throw 'No se pudo generar el hash con Docker (caddy hash-password).' }

# Entre comillas simples el .env es literal: Compose no interpola los '$' del hash
$lineaUsuario = "AUTH_USUARIO=$Usuario"
$lineaHash = "AUTH_HASH='$hash'"

if (-not $ActualizarEnv) {
    Write-Host "`nAgregue estas lineas al .env (o ejecute de nuevo con -ActualizarEnv):`n"
    Write-Host $lineaUsuario
    Write-Host $lineaHash
    return
}

if (-not (Test-Path $Env)) { throw "No existe $Env. Copie .env.example a .env primero." }
$lineas = [System.Collections.Generic.List[string]](Get-Content $Env)
foreach ($par in @(@('AUTH_USUARIO', $lineaUsuario), @('AUTH_HASH', $lineaHash))) {
    $i = $lineas.FindIndex({ param($l) $l -like "$($par[0])=*" })
    if ($i -ge 0) { $lineas[$i] = $par[1] } else { $lineas.Add($par[1]) }
}
[IO.File]::WriteAllText($Env, (($lineas -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))
Write-Host "Listo: AUTH_USUARIO y AUTH_HASH actualizados en $Env (la clave en claro no se guardo)." -ForegroundColor Green
