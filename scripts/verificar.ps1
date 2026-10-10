# Verificacion local: el mismo contrato que el CI (fase 5 del plan).
# Uso:
#   powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1            # completa: format + build + test (como el CI)
#   powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1 -Rapido    # format whitespace + build (~10 s; la usa el hook pre-push)
# En modo rapido solo se verifica el espacio en blanco (7 s en vez de 21 s); el estilo y los analizadores
# ya los exige el build con TreatWarningsAsErrors, y el formato completo corre en la verificacion completa y en CI.
# Se detiene en el primer fallo y devuelve su codigo de salida (0 = todo bien).

param([switch]$Rapido)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$solucion = Join-Path $raiz 'GrupoDigitalBank.slnx'
Set-Location $raiz

function Paso($nombre, [scriptblock]$accion) {
    Write-Host "`n== $nombre ==" -ForegroundColor Cyan
    $reloj = [Diagnostics.Stopwatch]::StartNew()
    & $accion
    $codigo = $LASTEXITCODE
    $reloj.Stop()
    if ($codigo -ne 0) {
        Write-Host ("FALLO: {0} ({1:n1} s, codigo {2})" -f $nombre, $reloj.Elapsed.TotalSeconds, $codigo) -ForegroundColor Red
        exit $codigo
    }
    Write-Host ("OK: {0} ({1:n1} s)" -f $nombre, $reloj.Elapsed.TotalSeconds) -ForegroundColor Green
}

Paso 'Restaurar paquetes' { dotnet restore $solucion --verbosity quiet }
if ($Rapido) {
    Paso 'Formato rapido (dotnet format whitespace)' { dotnet format whitespace $solucion --verify-no-changes --no-restore }
}
else {
    Paso 'Formato (dotnet format --verify-no-changes)' { dotnet format $solucion --verify-no-changes --no-restore }
}
Paso 'Compilacion (TreatWarningsAsErrors activo)' { dotnet build $solucion --no-restore --verbosity quiet --nologo }

if (-not $Rapido) {
    Paso 'Pruebas (dotnet test)' { dotnet test $solucion --no-build --nologo }
}

Write-Host "`nVerificacion correcta$(if ($Rapido) { ' (rapida: sin pruebas)' })." -ForegroundColor Green
exit 0
