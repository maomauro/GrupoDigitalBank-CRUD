# Verifica la documentacion: enlaces relativos entre archivos Markdown y, opcionalmente, los diagramas Mermaid.
#
#   powershell -ExecutionPolicy Bypass -File scripts\verificar-docs.ps1               # solo enlaces (rapido, sin dependencias)
#   powershell -ExecutionPolicy Bypass -File scripts\verificar-docs.ps1 -Diagramas    # ademas analiza y DIBUJA cada diagrama Mermaid
#
# Enlaces: comprueba que cada enlace relativo [texto](ruta) apunte a un archivo o carpeta que existe (no valida
# anclas #seccion ni URL externas). Se ignoran los bloques de codigo y el codigo en linea.
# Diagramas (opcional): requiere Node.js y Chrome o Edge; usa el motor real de Mermaid 11 dentro del navegador,
# la misma herramienta puppeteer-core de scripts\probar-navegador.ps1. Un diagrama con sintaxis rota hace fallar el script.
# Codigo de salida: 0 si todo esta bien, 1 si hay enlaces o diagramas rotos.

param(
    [switch]$Diagramas,
    [string]$Chrome = '',
    [string]$Raiz = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$fallos = 0

# ---------- 1) Enlaces relativos ----------
Write-Host "`n== Enlaces relativos ==" -ForegroundColor Cyan
$archivos = Get-ChildItem -Path $Raiz -Recurse -Filter *.md -File |
    Where-Object { $_.FullName -notmatch '\\(node_modules|bin|obj|TestResults|\.git)\\' }
$revisados = 0
$rotos = New-Object System.Collections.Generic.List[string]
foreach ($archivo in $archivos) {
    $texto = [IO.File]::ReadAllText($archivo.FullName)
    $texto = [regex]::Replace($texto, '(?s)```.*?```', '')       # bloques de codigo
    $texto = [regex]::Replace($texto, '`[^`\r\n]*`', '')          # codigo en linea
    foreach ($m in [regex]::Matches($texto, '\]\(([^)\s]+)\)')) {
        $destino = $m.Groups[1].Value
        if ($destino -match '^(https?:|mailto:|#)') { continue }
        $ruta = ($destino -split '#')[0]
        if (-not $ruta) { continue }
        $revisados++
        $completa = Join-Path $archivo.DirectoryName ($ruta -replace '/', '\')
        if (-not (Test-Path -LiteralPath $completa)) {
            $relativo = $archivo.FullName.Substring($Raiz.TrimEnd('\').Length + 1)
            $rotos.Add("$relativo  ->  $destino")
        }
    }
}
if ($rotos.Count -eq 0) {
    Write-Host ("OK     {0} enlaces relativos en {1} archivos, ninguno roto" -f $revisados, $archivos.Count) -ForegroundColor Green
}
else {
    $fallos += $rotos.Count
    Write-Host ("FALLA  {0} enlace(s) roto(s) de {1} revisados:" -f $rotos.Count, $revisados) -ForegroundColor Red
    $rotos | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
}

# ---------- 2) Diagramas Mermaid (opcional) ----------
if ($Diagramas) {
    Write-Host "`n== Diagramas Mermaid ==" -ForegroundColor Cyan
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'No se encontro Node.js (https://nodejs.org).' }
    if (-not $Chrome) {
        $Chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
                    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
                    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $Chrome) { throw 'No se encontro Chrome ni Edge; indique la ruta con -Chrome.' }
    $carpeta = Join-Path $PSScriptRoot 'navegador'
    Push-Location $carpeta
    try {
        if (-not (Test-Path 'node_modules\mermaid')) {
            Write-Host 'Instalando dependencias (una sola vez)...' -ForegroundColor DarkGray
            $env:PUPPETEER_SKIP_DOWNLOAD = 'true'
            npm ci --silent --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw 'npm ci fallo.' }
        }
        # @(...) fuerza una lista: con un solo archivo PowerShell devolveria una cadena y @cadena la repartiria letra por letra
        $conDiagramas = @($archivos | Where-Object { Select-String -Path $_.FullName -Pattern '^```mermaid' -Quiet } | ForEach-Object { $_.FullName })
        if ($conDiagramas.Count -eq 0) { Write-Host 'No hay diagramas Mermaid.' }
        else {
            $env:CHROME_PATH = $Chrome
            node mermaid-check.js @conDiagramas
            if ($LASTEXITCODE -ne 0) { $fallos++ }
        }
    }
    finally {
        Remove-Item Env:CHROME_PATH -ErrorAction SilentlyContinue
        Pop-Location
    }
}

Write-Host ''
if ($fallos -eq 0) { Write-Host 'Documentacion: correcta.' -ForegroundColor Green; exit 0 }
Write-Host "Documentacion: $fallos problema(s)." -ForegroundColor Red; exit 1
