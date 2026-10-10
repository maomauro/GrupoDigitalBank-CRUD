# Mide la cobertura de pruebas y exige un minimo en la logica de Negocios (hallazgo H7, meta >= 80 %).
# Ejecuta TODAS las pruebas con coverlet, une los resultados de los tres proyectos de prueba y genera un informe.
# Uso:
#   powershell -ExecutionPolicy Bypass -File scripts\cobertura.ps1                    # exige 80 % en Negocios
#   powershell -ExecutionPolicy Bypass -File scripts\cobertura.ps1 -Umbral 85 -Abrir   # otro umbral y abre el HTML
#   powershell -ExecutionPolicy Bypass -File scripts\cobertura.ps1 -SinUmbral          # solo informa
# Requiere Docker (las pruebas de integracion usan PostgreSQL en un contenedor).
# Salida: TestResults\informe\index.html y TestResults\informe\Summary.txt (carpeta ignorada por Git).
# Codigo 0 si se cumple el umbral, 1 si no, y el de dotnet si fallan las pruebas.

param(
    [double]$Umbral = 80,
    [switch]$SinUmbral,
    [switch]$Abrir
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
$resultados = Join-Path $raiz 'TestResults'
$informe = Join-Path $resultados 'informe'

Remove-Item $resultados -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n== Pruebas con cobertura ==" -ForegroundColor Cyan
dotnet test GrupoDigitalBank.slnx --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory $resultados
if ($LASTEXITCODE -ne 0) { Write-Host 'Las pruebas fallaron: no se calcula la cobertura.' -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "`n== Informe ==" -ForegroundColor Cyan
dotnet tool restore --verbosity quiet | Out-Null
$archivos = Get-ChildItem $resultados -Recurse -Filter 'coverage.cobertura.xml'
Write-Host "Se unen $($archivos.Count) archivos de cobertura (uno por proyecto de prueba)."
dotnet reportgenerator "-reports:$resultados/**/coverage.cobertura.xml" "-targetdir:$informe" `
    '-reporttypes:TextSummary;Html;Cobertura' '-assemblyfilters:+GrupoDigitalBank.*;-*.Tests;-*.IntegrationTests' '-verbosity:Warning'
if ($LASTEXITCODE -ne 0) { throw 'reportgenerator fallo.' }

# Cobertura de lineas por ensamblado, leida del Cobertura combinado
[xml]$xml = Get-Content (Join-Path $informe 'Cobertura.xml')
$filas = foreach ($p in $xml.coverage.packages.package) {
    $validas = 0; $cubiertas = 0
    foreach ($c in $p.classes.class) { foreach ($l in $c.lines.line) { $validas++; if ([int]$l.hits -gt 0) { $cubiertas++ } } }
    [pscustomobject]@{ Ensamblado = $p.name; Lineas = $validas; Cubiertas = $cubiertas; Porcentaje = if ($validas) { [math]::Round(100.0 * $cubiertas / $validas, 1) } else { 0 } }
}
Write-Host ''
$filas | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Informe HTML: $(Join-Path $informe 'index.html')"

if ($Abrir) { Start-Process (Join-Path $informe 'index.html') }
if ($SinUmbral) { exit 0 }

$negocios = $filas | Where-Object { $_.Ensamblado -eq 'GrupoDigitalBank.Negocios' }
if (-not $negocios) { Write-Host 'No se encontro el ensamblado GrupoDigitalBank.Negocios en la cobertura.' -ForegroundColor Red; exit 1 }
if ($negocios.Porcentaje -ge $Umbral) {
    Write-Host ("Cobertura de Negocios: {0} % (minimo {1} %): CUMPLE." -f $negocios.Porcentaje, $Umbral) -ForegroundColor Green; exit 0
}
Write-Host ("Cobertura de Negocios: {0} % (minimo {1} %): NO CUMPLE." -f $negocios.Porcentaje, $Umbral) -ForegroundColor Red
exit 1
