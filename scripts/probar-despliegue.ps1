# Ejecuta scripts/probar-despliegue.sh (prueba de punta a punta del despliegue, ADR 0011) desde Windows o macOS.
# El guion necesita Linux: aqui se corre dentro de un contenedor con el socket de Docker. Las carpetas de trabajo se
# montan con la MISMA ruta dentro y fuera del contenedor, porque Docker Compose le pasa al servicio de Docker rutas del
# contenedor y el servicio las resuelve en la maquina virtual (por eso /tmp/gdbprueba, no una carpeta de Windows).
#
#   powershell -ExecutionPolicy Bypass -File scripts\probar-despliegue.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\probar-despliegue.ps1 -Raiz C:\ruta\a\otra\copia
#
# Requiere Docker Desktop. Tarda unos 7 minutos. No toca nada real: todo lleva el prefijo "gdbprueba" y se elimina.
# Codigo de salida: el del guion (0 = todo bien).

param(
    [string]$Raiz = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$trabajo = '/tmp/gdbprueba'

# Carpeta de trabajo limpia en la maquina virtual de Docker
docker run --rm -v "${trabajo}:/t" alpine sh -c 'rm -rf /t/* /t/.[!.]* 2>/dev/null; true' | Out-Null

$orden = 'apk add -q --no-cache bash python3 coreutils findutils grep sed tar gzip curl >/dev/null 2>&1 && bash scripts/probar-despliegue.sh'
docker run --rm `
    -v /var/run/docker.sock:/var/run/docker.sock `
    -v "${trabajo}:${trabajo}" `
    -v "${Raiz}:/repo:ro" `
    -e "GDB_PRUEBA_DIR=$trabajo" `
    -w /repo docker:cli sh -c $orden
$codigo = $LASTEXITCODE

docker run --rm -v "${trabajo}:/t" alpine sh -c 'rm -rf /t/* /t/.[!.]* 2>/dev/null; true' | Out-Null
exit $codigo
