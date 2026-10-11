#!/bin/bash
# Instala en el servidor los scripts que ejecuta la llave de despliegue, con dueno root y fuera de la carpeta del
# proyecto: un paquete de despliegue no puede reemplazarlos (ver ADR 0011). Hay que repetirlo cuando cambien en el repo
# (los cambios llegan por un PR revisado; esto NO se actualiza solo).
#
#   sudo bash instalar.sh                 # instala en /usr/local/sbin
#   bash instalar.sh --destino /ruta      # solo para pruebas: instala en otra carpeta sin cambiar el dueno
#
# Se ejecuta desde la carpeta deploy/ copiada al servidor (por ejemplo con scp).
set -euo pipefail

aqui=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
destino=/usr/local/sbin
prueba=0
if [[ "${1:-}" = "--destino" ]]; then
    destino=${2:?Falta la carpeta de destino}
    prueba=1
fi

command -v python3 > /dev/null || { echo "Hace falta python3 (lo usa gdb-crud-politica.py)." >&2; exit 1; }
command -v docker > /dev/null || { echo "Hace falta docker." >&2; exit 1; }

mkdir -p "$destino"
# origen -> nombre instalado
instalar() {
    local origen=$1 nombre=$2
    if [[ "$prueba" = 1 ]]; then
        install -m 755 "$aqui/$origen" "$destino/$nombre"
    else
        install -m 755 -o root -g root "$aqui/$origen" "$destino/$nombre"
    fi
    printf '%s  %s\n' "$(sha256sum "$destino/$nombre" | cut -d' ' -f1)" "$destino/$nombre"
}
instalar entrada.sh gdb-crud-entrada.sh
instalar desplegar.sh gdb-crud-desplegar.sh
instalar backup.sh gdb-crud-backup.sh
instalar backup-externo.sh gdb-crud-backup-externo.sh
instalar probar-restauracion.sh gdb-crud-probar-restauracion.sh
instalar politica.py gdb-crud-politica.py
echo "Instalado en $destino. Compare las sumas con las del repositorio antes de seguir."
