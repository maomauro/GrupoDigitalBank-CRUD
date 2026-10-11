#!/bin/bash
# Revisa el estado operativo de GDB y avisa a un servicio de "latido" (Healthchecks.io):
#   - todo bien   -> hace ping a la direccion del latido (el servicio espera uno cada dia);
#   - algo falla  -> hace ping a <direccion>/fail con la lista de problemas (aviso inmediato por correo);
#   - el script no corre (cron caido, servidor apagado) -> no llega el latido y el servicio avisa pasada la tolerancia.
#
# Comprobaciones:
#   1. la copia local mas reciente existe, es reciente (MAX_EDAD_BACKUP_HORAS, 26 por omision), su suma coincide y el gzip es valido;
#   2. el disco donde viven los datos no pasa de MAX_DISCO_PORCIENTO (85);
#   3. los contenedores de GDB estan sanos (healthy);
#   4. (si RCLONE_REMOTE esta definido) la copia externa cifrada mas reciente tiene menos de MAX_EDAD_EXTERNA_DIAS (35);
#   5. (si URL_SALUD esta definida) la web responde por internet.
#
# Cron del VPS (hora de Berlin; despues del backup diario de las 02:45):
#   0 5 * * * RCLONE_REMOTE=b2backup:mi-bucket/gdb-crud URL_SALUD=https://gdb-crud.sitiosapps.com/health /usr/local/sbin/gdb-crud-alertas.sh >> /srv/sitiosapps/_backups/gdb-crud/alertas.log 2>&1
#
# La direccion del latido es SECRETA (quien la conozca puede enviar latidos falsos): vive en un archivo con permiso 600
# (HEARTBEAT_URL_FILE, por omision ~/.config/gdb-crud/heartbeat-url), nunca se imprime y se pasa a curl por la entrada
# estandar para que no aparezca en la lista de procesos.
#
# Variables para pruebas: BACKUP_DIR, DATA_DIR, CONTENEDORES.
set -uo pipefail

DESTINO=${BACKUP_DIR:-/srv/sitiosapps/_backups/gdb-crud}
DATOS=${DATA_DIR:-/srv}
ARCHIVO_URL=${HEARTBEAT_URL_FILE:-$HOME/.config/gdb-crud/heartbeat-url}
MAX_BACKUP_H=${MAX_EDAD_BACKUP_HORAS:-26}
MAX_DISCO=${MAX_DISCO_PORCIENTO:-85}
MAX_EXTERNA_D=${MAX_EDAD_EXTERNA_DIAS:-35}
read -r -a CONTENEDORES <<< "${CONTENEDORES:-gdb-crud-db-1 gdb-crud-negocios-1 gdb-crud-web-1 gdb-crud-proxy-1}"

problemas=()
registrar() { problemas+=("$1"); }
ahora=$(date +%s)

# ---- 1. copia local ----
copia=$(find "$DESTINO" -maxdepth 1 -type f -name 'usuarios_*.sql.gz' -printf '%T@ %p\n' 2> /dev/null | sort -rn | sed -n '1p' | cut -d' ' -f2-)
if [[ -z "$copia" ]]; then
    registrar "no hay copias locales de la base en $DESTINO"
else
    edad_h=$(((ahora - $(stat -c %Y "$copia")) / 3600))
    if [[ "$edad_h" -gt "$MAX_BACKUP_H" ]]; then
        registrar "la copia local mas reciente tiene $edad_h h (maximo $MAX_BACKUP_H): el backup diario no se esta generando"
    fi
    if [[ -f "$copia.sha256" ]] && ! (cd "$(dirname "$copia")" && sha256sum -c "$(basename "$copia").sha256" > /dev/null 2>&1); then
        registrar "la suma de verificacion de $(basename "$copia") no coincide"
    fi
    gzip -t "$copia" 2> /dev/null || registrar "la copia $(basename "$copia") esta danada"
fi

# ---- 2. disco ----
uso=$(df -P "$DATOS" 2> /dev/null | awk 'NR == 2 { gsub("%", "", $5); print $5 }')
if [[ -z "$uso" ]]; then
    registrar "no se pudo leer el uso del disco de $DATOS"
elif [[ "$uso" -gt "$MAX_DISCO" ]]; then
    registrar "el disco esta al $uso % (maximo $MAX_DISCO %)"
fi

# ---- 3. contenedores ----
for c in "${CONTENEDORES[@]}"; do
    estado=$(docker inspect -f '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}sin-healthcheck{{end}}' "$c" 2> /dev/null || echo "no-existe")
    [[ "$estado" == "running healthy" ]] || registrar "el contenedor $c no esta sano ($estado)"
done

# ---- 4. copia externa (opcional) ----
if [[ -n "${RCLONE_REMOTE:-}" ]]; then
    ultima=$(rclone lsf "$RCLONE_REMOTE" --files-only --format "tp" --separator "|" --include 'usuarios_*.age' 2> /dev/null | cut -d'|' -f1 | sort | tail -n1)
    if [[ -z "$ultima" ]]; then
        registrar "no se pudo leer la copia externa en el almacen remoto (o no hay ninguna)"
    else
        edad_d=$(((ahora - $(date -d "$ultima" +%s 2> /dev/null || echo 0)) / 86400))
        [[ "$edad_d" -le "$MAX_EXTERNA_D" ]] || registrar "la copia externa mas reciente tiene $edad_d dias (maximo $MAX_EXTERNA_D)"
    fi
fi

# ---- 5. la web por internet (opcional) ----
if [[ -n "${URL_SALUD:-}" ]]; then
    curl -fsS -m 15 -o /dev/null "$URL_SALUD" 2> /dev/null || registrar "la web no responde por internet ($URL_SALUD)"
fi

# ---- informar ----
marca=$(date '+%F %T')
if [[ "${#problemas[@]}" -eq 0 ]]; then
    echo "$marca OK: copia local, disco ($uso %), contenedores${RCLONE_REMOTE:+, copia externa}${URL_SALUD:+, web}"
    resultado=ok
else
    resultado=falla
    for p in "${problemas[@]}"; do echo "$marca PROBLEMA: $p"; done
fi

if [[ ! -s "$ARCHIVO_URL" ]]; then
    echo "$marca ADVERTENCIA: no hay direccion de latido en $ARCHIVO_URL; no se avisa a nadie" >&2
    exit 1
fi
url=$(< "$ARCHIVO_URL")
url=${url%/}
if [[ "$resultado" == ok ]]; then
    destino_ping=$url
    cuerpo=""
else
    destino_ping=$url/fail
    cuerpo=$(printf '%s; ' "${problemas[@]}" | tr -d '"\\\n\r' | cut -c1-900)
fi
if printf 'url = "%s"\ndata = "%s"\n' "$destino_ping" "$cuerpo" | curl -fsS -m 15 --retry 3 -o /dev/null --config - 2> /dev/null; then
    echo "$marca latido enviado ($resultado)"
else
    echo "$marca ADVERTENCIA: no se pudo enviar el latido" >&2
    exit 1
fi
[[ "$resultado" == ok ]]
