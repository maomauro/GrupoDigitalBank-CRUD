# Ejecuta scripts/probar-backup-externo.sh desde Windows o macOS dentro de un contenedor Linux (necesita bash, age y rclone).
#
#   powershell -ExecutionPolicy Bypass -File scripts\probar-backup-externo.ps1
#
# Requiere Docker Desktop. No toca nada real: todo vive en una carpeta temporal del contenedor. Codigo de salida: el del guion.
param(
    [string]$Raiz = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$orden = 'apk add -q --no-cache bash age rclone coreutils findutils grep sed gzip >/dev/null 2>&1 && bash scripts/probar-backup-externo.sh'
docker run --rm -v "${Raiz}:/repo:ro" -w /repo alpine sh -c $orden
exit $LASTEXITCODE
