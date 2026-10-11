#!/bin/bash
# Despliega UNA version de este proyecto en el VPS compartido. Lo ejecuta deploy/entrada.sh (la unica orden que puede
# ejecutar la llave de GitHub Actions) con el paquete ya extraido en una carpeta de preparacion. Ver ADR 0011.
#
#   gdb-crud-desplegar <tag> <carpeta-de-la-entrega>
#
# Que hace, en orden:
#   1. Valida la entrega con la politica de seguridad (gdb-crud-politica): un compose arbitrario equivaldria a root.
#   2. Copia de seguridad de la base (si ya esta corriendo).
#   3. Descarga las imagenes y levanta el stack esperando a que todo este sano (el migrador corre antes que Negocios).
#   4. Si algo falla, vuelve a la version anterior. Las migraciones NO se deshacen (son expand/contract, ADR 0007).
#
# Disposicion en el servidor ($GDB_DIR, por omision /srv/sitiosapps/gdb-crud):
#   .env                       secretos; nunca se escribe desde aqui
#   releases/<tag>/            compose, Caddyfile y db/ de cada version (el compose usa SIEMPRE la carpeta de su version)
#   releases/ACTUAL            tag de la version en marcha
#
# Variables de entorno (solo para las pruebas locales, scripts/probar-despliegue.sh; en produccion no se definen):
#   GDB_DIR, GDB_SBIN, GDB_PROYECTO, REGISTRO, RED_COMPARTIDA.
set -euo pipefail

DIR=${GDB_DIR:-/srv/sitiosapps/gdb-crud}
SBIN=${GDB_SBIN:-/usr/local/sbin}
PROYECTO=${GDB_PROYECTO:-gdb-crud}
REGISTRO=${REGISTRO:-ghcr.io}
RED_COMPARTIDA=${RED_COMPARTIDA:-curriculum-vitae-web_portalcv-net-prod}
export REGISTRO RED_COMPARTIDA
# En un runner de GitHub Actions GITHUB_REPOSITORY ya existe (con mayusculas) y, en Compose, el entorno gana al .env:
# las imagenes saldrian con otro nombre que el que valida la politica. El .env es la unica fuente.
unset GITHUB_REPOSITORY
CONSERVAR=5

if [ $# -ne 2 ]; then
    echo "Uso: $0 <tag> <carpeta-de-la-entrega>" >&2
    exit 2
fi
tag=$1
entrega=$2
if ! [[ $tag =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]]; then
    echo "Tag no valido: $tag" >&2
    exit 2
fi
[ -f "$DIR/.env" ] || { echo "Falta $DIR/.env (ver docs/plan-de-trabajo.md, 6.3)" >&2; exit 2; }
repositorio=$(grep -E '^GITHUB_REPOSITORY=' "$DIR/.env" | tail -n1 | cut -d= -f2-)
[ -n "$repositorio" ] || { echo "Falta GITHUB_REPOSITORY en $DIR/.env" >&2; exit 2; }

# Bloqueo: un solo despliegue a la vez. Si un proceso muerto a la fuerza lo dejara colgado, se borra a mano.
mkdir -p "$DIR/releases"
BLOQUEO="$DIR/releases/.bloqueo"
if ! mkdir "$BLOQUEO" 2>/dev/null; then
    echo "Ya hay un despliegue en curso (o quedo un bloqueo viejo: $BLOQUEO)." >&2
    exit 1
fi
trabajo=$(mktemp -d)
nueva=$DIR/releases/$tag
limpiar() {
    rm -rf "$trabajo" "$BLOQUEO"
    # La entrega que no llego a instalarse no se deja a medias
    [ -d "$entrega" ] && rm -rf "$entrega"
    return 0
}
trap limpiar EXIT

# docker compose de una version concreta; IMAGE_TAG viaja por entorno y gana al del .env
compose() {
    local version=$1 dir_version=$DIR/releases/$1
    shift
    IMAGE_TAG=$version docker compose -p "$PROYECTO" --project-directory "$dir_version" --env-file "$DIR/.env" \
        -f "$dir_version/docker-compose.yml" -f "$dir_version/docker-compose.prod.yml" "$@"
}

previa=""
[ -f "$DIR/releases/ACTUAL" ] && previa=$(cat "$DIR/releases/ACTUAL")

# ---- 1. Instalar la entrega como release y validarla ----
if [ -e "$nueva" ] && [ "$tag" != "$previa" ]; then rm -rf "$nueva"; fi   # un tag que no esta en marcha se reemplaza
if [ "$tag" = "$previa" ]; then
    # Redespliegue de la version en marcha: no se pisa su carpeta hasta saber que la nueva entrega es valida
    destino=$trabajo/release
else
    destino=$nueva
fi
mkdir -p "$destino"
cp -a "$entrega"/. "$destino"/
chmod -R go+rX,go-w "$destino"

validar_politica() {
    local dir=$1
    IMAGE_TAG=$tag docker compose -p "$PROYECTO" --project-directory "$dir" --env-file "$DIR/.env" \
        -f "$dir/docker-compose.yml" -f "$dir/docker-compose.prod.yml" config --format json > "$trabajo/config.json"
    python3 "$SBIN/gdb-crud-politica.py" "$trabajo/config.json" "$dir" "$REGISTRO" "$repositorio"
}
if ! validar_politica "$destino"; then
    echo "La entrega no cumple la politica de seguridad: no se despliega nada." >&2
    [ "$destino" = "$nueva" ] && rm -rf "$nueva"
    exit 3
fi
if [ "$destino" != "$nueva" ]; then
    rm -rf "$nueva"
    mv "$destino" "$nueva"
fi

# ---- 2. Copia de seguridad previa (el migrador puede cambiar el esquema) ----
if [ -n "$previa" ] && compose "$previa" ps --status running --services 2>/dev/null | grep -qx db; then
    echo "== Copia de seguridad previa =="
    bash "$SBIN/gdb-crud-backup.sh"
fi

# ---- 3. Descargar y levantar ----
echo "== Descargando imagenes ($tag) =="
if ! compose "$tag" pull --quiet; then
    echo "No se pudieron descargar las imagenes: no se toco nada de lo que esta en marcha." >&2
    [ "$tag" != "$previa" ] && rm -rf "$nueva"
    exit 1
fi

echo "== Levantando $tag =="
if ! compose "$tag" up -d --remove-orphans --wait --wait-timeout 180; then
    echo "FALLO el despliegue de $tag." >&2
    compose "$tag" ps >&2 || true
    if [ -n "$previa" ] && [ "$previa" != "$tag" ]; then
        echo "== Volviendo a $previa ==" >&2
        if compose "$previa" up -d --remove-orphans --wait --wait-timeout 180; then
            echo "Rollback correcto: sigue en marcha $previa." >&2
        else
            echo "EL ROLLBACK TAMBIEN FALLO: hay que intervenir a mano." >&2
        fi
    elif [ -z "$previa" ]; then
        echo "Primer despliegue: se detiene el stack (los volumenes se conservan)." >&2
        compose "$tag" down || true
    fi
    exit 1
fi

# ---- 4. Registrar la version y podar las viejas ----
printf '%s\n' "$tag" > "$DIR/releases/ACTUAL.tmp"
mv "$DIR/releases/ACTUAL.tmp" "$DIR/releases/ACTUAL"
# Se conservan las ultimas $CONSERVAR versiones por fecha de modificacion; la actual y la anterior nunca se borran
mapfile -t viejas < <(find "$DIR/releases" -mindepth 1 -maxdepth 1 -type d -not -name '.*' -printf '%T@ %f\n' 2>/dev/null \
    | sort -rn | awk -v n="$CONSERVAR" 'NR>n {print $2}')
for v in "${viejas[@]}"; do
    [ "$v" = "$tag" ] || [ "$v" = "$previa" ] || rm -rf "${DIR:?}/releases/$v"
done
echo "== Listo: $tag en marcha =="
compose "$tag" ps
