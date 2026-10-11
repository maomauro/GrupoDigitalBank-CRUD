#!/bin/bash
# Copia EXTERNA de la base y de la configuracion: toma la copia local mas reciente (la que deja backup.sh), la CIFRA con age
# y la sube a un almacen remoto (rclone), conservando solo las ultimas COPIAS_EXTERNAS (2 por omision). Si el VPS se pierde,
# las copias locales se pierden con el; estas no. Se descifran con la llave PRIVADA, que NO esta en el servidor.
#
# Cron mensual del VPS (hora de Berlin; el dia 1 a las 04:00, despues del backup diario de las 02:45):
#   0 4 1 * * RCLONE_REMOTE=b2backup:mi-bucket/gdb-crud /usr/local/sbin/gdb-crud-backup-externo.sh >> /srv/sitiosapps/_backups/gdb-crud/externo.log 2>&1
#
# Variables: RCLONE_REMOTE (obligatoria; destino de rclone, p. ej. b2backup:mi-bucket/gdb-crud), BACKUP_DIR, GDB_DIR,
# AGE_RECIPIENT_FILE (archivo con la llave PUBLICA de age; por omision /usr/local/etc/gdb-crud-age-recipient.txt),
# COPIAS_EXTERNAS (2), MAX_EDAD_DIAS (3: si la copia local mas reciente es mas vieja, el backup diario esta fallando).
# Restauracion: ver docs/backups.md.
set -euo pipefail
umask 077

REMOTO=${RCLONE_REMOTE:?Falta RCLONE_REMOTE (p. ej. b2backup:mi-bucket/gdb-crud)}
DIR=${GDB_DIR:-/srv/sitiosapps/gdb-crud}
DESTINO=${BACKUP_DIR:-/srv/sitiosapps/_backups/gdb-crud}
DESTINATARIO=${AGE_RECIPIENT_FILE:-/usr/local/etc/gdb-crud-age-recipient.txt}
CONSERVAR=${COPIAS_EXTERNAS:-2}
MAX_EDAD=${MAX_EDAD_DIAS:-3}

fallar() {
    echo "ERROR: $1" >&2
    exit 1
}

command -v age > /dev/null || fallar "falta age (apt-get install age)"
command -v rclone > /dev/null || fallar "falta rclone (apt-get install rclone)"
[[ -s "$DESTINATARIO" ]] || fallar "no existe la llave publica de age en $DESTINATARIO"
[[ "$CONSERVAR" =~ ^[1-9][0-9]*$ ]] || fallar "COPIAS_EXTERNAS debe ser un entero positivo"

# 1) La copia local mas reciente, integra y reciente
# (sed lee toda la entrada: con head -n1 y pipefail, un SIGPIPE intermitente haria fallar el script)
copia=$(find "$DESTINO" -maxdepth 1 -type f -name 'usuarios_*.sql.gz' -printf '%T@ %p\n' | sort -rn | sed -n '1p' | cut -d' ' -f2-)
[[ -n "$copia" ]] || fallar "no hay copias locales en $DESTINO"
edad=$((($(date +%s) - $(stat -c %Y "$copia")) / 86400))
[[ "$edad" -le "$MAX_EDAD" ]] || fallar "la copia local mas reciente tiene $edad dias (maximo $MAX_EDAD): el backup diario esta fallando"
if [[ -f "$copia.sha256" ]]; then
    (cd "$(dirname "$copia")" && sha256sum -c "$(basename "$copia").sha256" > /dev/null) || fallar "la suma de verificacion de $copia no coincide"
fi
gzip -t "$copia" || fallar "$copia esta danada"

# 2) Cifrar en una carpeta temporal privada que se borra siempre
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
nombre_copia=$(basename "$copia").age
nombre_env=env_$(date +%F_%H%M%S).age
age -R "$DESTINATARIO" -o "$tmp/$nombre_copia" "$copia" || fallar "no se pudo cifrar la copia (llave publica invalida?)"
if [[ -f "$DIR/.env" ]]; then
    age -R "$DESTINATARIO" -o "$tmp/$nombre_env" "$DIR/.env" || fallar "no se pudo cifrar el .env"
else
    echo "ADVERTENCIA: no existe $DIR/.env; se sube solo la base." >&2
fi

# 3) Subir y comprobar que lo remoto coincide con lo local (hash) antes de rotar nada
rclone copy "$tmp" "$REMOTO" || fallar "fallo la subida a $REMOTO"
rclone check "$tmp" "$REMOTO" --one-way > /dev/null 2>&1 || fallar "lo subido no coincide con lo local: no se rota nada"

# 4) Rotacion: conservar solo las ultimas N de cada tipo (los nombres llevan fecha, el orden alfabetico es el cronologico).
#    --b2-hard-delete: en B2 borrar sin esto deja versiones ocultas que siguen ocupando espacio.
rotar() {
    local patron=$1 sobrantes
    sobrantes=$(rclone lsf "$REMOTO" --files-only --include "$patron" | sort | awk -v n="$CONSERVAR" '{ a[NR] = $0 } END { for (i = 1; i <= NR - n; i++) print a[i] }')
    while IFS= read -r archivo; do
        [[ -n "$archivo" ]] || continue
        rclone deletefile "$REMOTO/$archivo" --b2-hard-delete
        echo "Borrada la copia externa antigua: $archivo"
    done <<< "$sobrantes"
}
rotar 'usuarios_*.sql.gz.age'
rotar 'env_*.age'

echo "Copia externa lista: $nombre_copia en $REMOTO (se conservan las ultimas $CONSERVAR)."
