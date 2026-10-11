#!/bin/bash
# Copia de seguridad de la base de datos (pg_dump comprimido, con suma de verificacion y rotacion). Sigue el patron
# de Colfe (deploy/backup.sh en su repositorio): volcado no interactivo con `docker compose exec -T`, gzip,
# sha256sum, verificacion de integridad, rotacion por RETENTION_DAYS y copia externa opcional con rclone.
#
# Lo ejecutan el cron del usuario del servidor y deploy/desplegar.sh antes de cada despliegue:
#   45 2 * * * /usr/local/sbin/gdb-crud-backup.sh >> /srv/sitiosapps/_backups/gdb-crud/backup.log 2>&1
# (las horas del cron del VPS son hora de Berlin: no chocan con las de PortalCV, 03:00 fin de mes, ni Colfe, 02:15 dom.)
#
# Variables opcionales: BACKUP_DIR, RETENTION_DAYS (14 por omision), RCLONE_REMOTE (p. ej. b2:mi-bucket/gdb-crud;
# sin definir no se copia fuera del servidor: la regla 3-2-1 queda pendiente de configurar).
# Para las pruebas locales: GDB_DIR, GDB_PROYECTO, REGISTRO, RED_COMPARTIDA.
set -euo pipefail
umask 077

DIR=${GDB_DIR:-/srv/sitiosapps/gdb-crud}
PROYECTO=${GDB_PROYECTO:-gdb-crud}
DESTINO=${BACKUP_DIR:-/srv/sitiosapps/_backups/gdb-crud}
RETENCION=${RETENTION_DAYS:-14}
export REGISTRO=${REGISTRO:-ghcr.io}
export RED_COMPARTIDA=${RED_COMPARTIDA:-curriculum-vitae-web_portalcv-net-prod}

version=$(cat "$DIR/releases/ACTUAL" 2>/dev/null || true)
[ -n "$version" ] || { echo "No hay una version desplegada: no hay nada que respaldar." >&2; exit 1; }
release=$DIR/releases/$version

compose() {
    IMAGE_TAG=$version docker compose -p "$PROYECTO" --project-directory "$release" --env-file "$DIR/.env" \
        -f "$release/docker-compose.yml" -f "$release/docker-compose.prod.yml" "$@"
}

mkdir -p "$DESTINO"
archivo=$DESTINO/usuarios_$(date +%F_%H%M%S).sql.gz
temporal=$archivo.tmp
trap 'rm -f "$temporal"' EXIT

# Las credenciales son las del propio contenedor (no se hace `source .env`: el hash lleva signos de dolar)
# Las comillas simples son a proposito: las variables las expande el shell DEL CONTENEDOR, no este
# shellcheck disable=SC2016
compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner' | gzip > "$temporal"

# Integridad: el gzip se lee completo y el volcado contiene el esquema (un volcado vacio o truncado no sirve)
gzip -t "$temporal"
if ! gzip -dc "$temporal" | grep -q 'CREATE TABLE public.usuario'; then
    echo "El volcado no contiene la tabla usuario: no se conserva." >&2
    exit 1
fi

mv "$temporal" "$archivo"
( cd "$DESTINO" && sha256sum "$(basename "$archivo")" > "$(basename "$archivo").sha256" )
echo "Copia creada: $archivo ($(du -h "$archivo" | cut -f1))"

# Rotacion: borra los volcados (y su suma) mas viejos que RETENCION dias
find "$DESTINO" -maxdepth 1 -type f \( -name 'usuarios_*.sql.gz' -o -name 'usuarios_*.sql.gz.sha256' \) -mtime +"$RETENCION" -delete

# Copia fuera del servidor (3-2-1), opcional
if [ -n "${RCLONE_REMOTE:-}" ]; then
    if command -v rclone > /dev/null; then
        rclone copy "$archivo" "$RCLONE_REMOTE" && rclone copy "$archivo.sha256" "$RCLONE_REMOTE"
        echo "Copia externa enviada a $RCLONE_REMOTE"
    else
        echo "ADVERTENCIA: RCLONE_REMOTE definido pero rclone no esta instalado." >&2
    fi
fi
