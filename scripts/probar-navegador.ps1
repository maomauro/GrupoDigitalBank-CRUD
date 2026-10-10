# Comprobaciones en un Chrome REAL contra el stack con el proxy (hallazgos B4 y H1):
#   - la CSP no produce violaciones en ninguna pagina y el CSS/JS externos funcionan;
#   - control positivo: el detector SI registra violaciones en una pagina que las provoca a proposito;
#   - regresion de H1 con clic real: un usuario llamado  x');alert(1);//  no ejecuta nada y el confirm
#     muestra el nombre literal. Crea y elimina SOLO ese usuario de prueba.
#
# Es una herramienta OPCIONAL: requiere Node.js, Google Chrome y Docker (para la pagina de control).
# Requisitos: el stack levantado con el perfil del proxy y AUTH_USUARIO/AUTH_HASH en .env, p. ej.:
#   docker compose -f docker-compose.yml -f docker-compose.dev.yml --profile con-proxy up -d --build --wait
# Uso:
#   powershell -ExecutionPolicy Bypass -File scripts\probar-navegador.ps1 -Usuario admin     (pide la clave)

param(
    [string]$Base = 'https://localhost',
    [string]$Usuario = 'admin',
    [securestring]$Clave,
    [string]$Chrome = ''
)

$ErrorActionPreference = 'Stop'
$carpeta = Join-Path $PSScriptRoot 'navegador'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'No se encontro Node.js (https://nodejs.org).' }
if (-not $Chrome) {
    $Chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
                "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
                "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Chrome) { throw 'No se encontro Chrome ni Edge; indique la ruta con -Chrome.' }
if (-not $Clave) { $Clave = Read-Host "Clave de '$Usuario'" -AsSecureString }
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Clave)
$claveTexto = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)

Write-Host "Navegador: $Chrome" -ForegroundColor DarkGray
Push-Location $carpeta
try {
    if (-not (Test-Path 'node_modules\puppeteer-core')) {
        Write-Host 'Instalando puppeteer-core (una sola vez, sin descargar un navegador)...' -ForegroundColor DarkGray
        $env:PUPPETEER_SKIP_DOWNLOAD = 'true'
        npm ci --silent --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci fallo.' }
    }

    # Pagina de control que viola la CSP a proposito, servida con la MISMA politica que el Caddyfile real
    $control = "gdb-control-csp-$([guid]::NewGuid().ToString('N').Substring(0, 6))"
    $cf = Join-Path ([IO.Path]::GetTempPath()) "$control.Caddyfile"
    $csp = (Select-String -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'Caddyfile') -Pattern '^\s*Content-Security-Policy\s+"(.+)"' | Select-Object -First 1).Matches[0].Groups[1].Value
    $contenido = ":8098 {`n`theader Content-Security-Policy `"$csp`"`n`theader Content-Type `"text/html; charset=utf-8`"`n" +
                 "`trespond `"<!doctype html><style>body{color:red}</style><p style='color:blue'>x</p><script>window.x=1</script><button onclick='window.y=1'>b</button>`" 200`n}`n"
    [IO.File]::WriteAllText($cf, $contenido)
    docker run -d --name $control -p 8098:8098 -v "${cf}:/etc/caddy/Caddyfile:ro" caddy:2-alpine | Out-Null
    Start-Sleep 3

    $env:CLAVE_WEB = $claveTexto
    $env:CHROME_PATH = $Chrome
    node csp.js $Base $Usuario 'http://localhost:8098/'
    $codigo = $LASTEXITCODE
}
finally {
    Remove-Item Env:CLAVE_WEB, Env:CHROME_PATH -ErrorAction SilentlyContinue
    if ($control) { docker rm -f $control 2>$null | Out-Null }
    if ($cf) { Remove-Item $cf -ErrorAction SilentlyContinue }
    Pop-Location
    $claveTexto = $null
}
exit $codigo
