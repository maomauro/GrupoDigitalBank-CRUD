#!/bin/bash
# Prueba deploy/alertas.sh sin tocar nada real: docker, df, curl y rclone son de mentira y cada caso arma un estado distinto
# (backup viejo, disco lleno, contenedor caido, copia externa vieja...). Comprueba que se avisa con /fail cuando algo falla, que
# el latido normal se envia solo si todo esta bien y que la direccion SECRETA del latido no aparece en la salida ni en los
# argumentos de curl.
#
#   bash scripts/probar-alertas.sh
set -uo pipefail

RAIZ=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT
fallos=0
SECRETO="https://hc-ping.example.test/SECRETO-uuid-1234"

mkdir -p "$T/bin" "$T/estado" "$T/copias" "$T/datos"
printf '%s\n' "$SECRETO" > "$T/url"

# --- herramientas de mentira ---
cat > "$T/bin/docker" << 'EOS'
#!/bin/bash
nombre=${@: -1}
if [[ -f "$ESTADO_DIR/$nombre" ]]; then
    cat "$ESTADO_DIR/$nombre"
    [[ "$(cat "$ESTADO_DIR/$nombre")" == "no-existe" ]] && exit 1
    exit 0
fi
echo "running healthy"
EOS
cat > "$T/bin/df" << 'EOS'
#!/bin/bash
echo "S.ficheros  1024-bloques Usado Disponible Capacidad Montado"
echo "/dev/x 100 50 50 ${DISCO_PORCIENTO:-40}% /srv"
EOS
cat > "$T/bin/curl" << 'EOS'
#!/bin/bash
# Registra los argumentos (nunca deben llevar la direccion secreta) y, si lee configuracion por stdin, tambien eso
echo "ARGS: $*" >> "$LOG_CURL"
if [[ "$*" == *"--config -"* ]]; then
    echo "STDIN: $(cat | tr '\n' ' ')" >> "$LOG_CURL"
    [[ -n "${PING_FALLA:-}" ]] && exit 22
    exit 0
fi
[[ -n "${SALUD_FALLA:-}" ]] && exit 22
exit 0
EOS
cat > "$T/bin/rclone" << 'EOS'
#!/bin/bash
echo "rclone $*" >> "$LOG_CURL"
cat "$LISTA_EXTERNA" 2> /dev/null
exit 0
EOS
chmod +x "$T/bin/"*

ok() { printf 'OK     %s\n' "$1"; }
falla() { printf 'FALLA  %s\n' "$1"; fallos=$((fallos + 1)); }

crear_copia() { # crear_copia <carpeta> <antiguedad>: copia valida con su suma
    mkdir -p "$1"
    local f="$1/usuarios_2026-10-11_000001.sql.gz"
    printf 'CREATE TABLE usuario (id int);\n' | gzip > "$f"
    (cd "$1" && sha256sum "$(basename "$f")" > "$(basename "$f").sha256")
    touch -d "$2" "$f"
}
estado_sano() { rm -f "$T/estado/"*; : > "$T/log.txt"; rm -f "$T/lista-externa.txt"; }

# correr [VAR=valor...]: ejecuta alertas.sh con el entorno de pruebas
correr() {
    : > "$T/log.txt"
    env PATH="$T/bin:$PATH" ESTADO_DIR="$T/estado" LOG_CURL="$T/log.txt" LISTA_EXTERNA="$T/lista-externa.txt" \
        BACKUP_DIR="$T/copias" DATA_DIR="$T/datos" HEARTBEAT_URL_FILE="$T/url" "$@" \
        bash "$RAIZ/deploy/alertas.sh" > "$T/salida.txt" 2>&1
}
ping_ok() { grep -q "STDIN: url = \"$SECRETO\"" "$T/log.txt"; }
ping_fail() { grep -q "STDIN: url = \"$SECRETO/fail\"" "$T/log.txt"; }
sin_ping() { ! grep -q "STDIN:" "$T/log.txt"; }

caso_ok() { # caso_ok <descripcion> [VAR=valor...]
    local d=$1
    shift
    if correr "$@" && ping_ok && ! ping_fail; then ok "$d"; else falla "$d (salida: $(tr '\n' '|' < "$T/salida.txt" | cut -c1-200))"; fi
}
caso_falla() { # caso_falla <descripcion> <texto del problema esperado> [VAR=valor...]
    local d=$1 texto=$2
    shift 2
    if correr "$@"; then falla "$d: debia fallar"
    elif ! ping_fail; then falla "$d: no aviso con /fail"
    elif ! grep -q "$texto" "$T/log.txt"; then falla "$d: el aviso no menciona '$texto'"
    elif grep -q "STDIN: url = \"$SECRETO\"[^/]" "$T/log.txt" && ping_ok; then falla "$d: ademas envio el latido de OK"
    else ok "$d"; fi
}

# ---- todo bien ----
estado_sano
crear_copia "$T/copias" "2 hours ago"
printf '%s|usuarios_2026-10-11_000001.sql.gz.age\n' "$(date -d '5 days ago' '+%F %T')" > "$T/lista-externa.txt"
caso_ok "todo bien: envia el latido normal" RCLONE_REMOTE=b2:bucket/gdb-crud URL_SALUD=https://x.test/health
if grep -q "SECRETO-uuid" "$T/salida.txt"; then falla "la direccion secreta aparece en la salida"; else ok "la direccion secreta NO aparece en la salida"; fi
if grep "^ARGS:" "$T/log.txt" | grep -q "SECRETO-uuid"; then falla "la direccion secreta aparece en los argumentos de curl"; else ok "la direccion secreta NO aparece en los argumentos de curl (va por stdin)"; fi
caso_ok "sin copia externa ni URL de salud configuradas, se omiten esas comprobaciones"
if grep -q "^rclone" "$T/log.txt"; then falla "llamo a rclone sin RCLONE_REMOTE"; else ok "sin RCLONE_REMOTE no llama a rclone"; fi

# ---- copia local ----
estado_sano; crear_copia "$T/copias" "30 hours ago"
caso_falla "backup de hace 30 horas" "backup diario no se esta generando"
estado_sano; crear_copia "$T/copias" "25 hours ago"
caso_ok "backup de hace 25 horas todavia es aceptable"
estado_sano; mkdir -p "$T/vacio"
caso_falla "sin copias locales" "no hay copias locales" BACKUP_DIR="$T/vacio"
estado_sano; crear_copia "$T/copias" "1 hour ago"; echo "0000000000000000000000000000000000000000000000000000000000000000  usuarios_2026-10-11_000001.sql.gz" > "$T/copias/usuarios_2026-10-11_000001.sql.gz.sha256"
caso_falla "suma de verificacion incorrecta" "suma de verificacion"
estado_sano; crear_copia "$T/copias" "1 hour ago"; echo "esto no es gzip" > "$T/copias/usuarios_2026-10-11_000001.sql.gz"; : > "$T/copias/usuarios_2026-10-11_000001.sql.gz.sha256"
caso_falla "copia danada" "danada"
crear_copia "$T/copias" "1 hour ago"

# ---- disco ----
estado_sano
caso_ok "disco al 85 % (en el limite) no avisa" DISCO_PORCIENTO=85
caso_falla "disco al 86 %" "disco esta al 86" DISCO_PORCIENTO=86

# ---- contenedores ----
estado_sano; echo "running unhealthy" > "$T/estado/gdb-crud-web-1"
caso_falla "contenedor web no sano" "gdb-crud-web-1"
estado_sano; echo "no-existe" > "$T/estado/gdb-crud-db-1"
caso_falla "contenedor de la base inexistente" "gdb-crud-db-1"
estado_sano; echo "exited sin-healthcheck" > "$T/estado/gdb-crud-proxy-1"
caso_falla "contenedor detenido" "gdb-crud-proxy-1"

# ---- copia externa ----
estado_sano
printf '%s|usuarios_x.sql.gz.age\n' "$(date -d '40 days ago' '+%F %T')" > "$T/lista-externa.txt"
caso_falla "copia externa de hace 40 dias" "copia externa mas reciente tiene 40" RCLONE_REMOTE=b2:b/g
printf '%s|usuarios_x.sql.gz.age\n' "$(date -d '34 days ago' '+%F %T')" > "$T/lista-externa.txt"
caso_ok "copia externa de hace 34 dias es aceptable" RCLONE_REMOTE=b2:b/g
: > "$T/lista-externa.txt"
caso_falla "almacen remoto sin copias o inaccesible" "no se pudo leer la copia externa" RCLONE_REMOTE=b2:b/g

# ---- web por internet ----
estado_sano
caso_falla "la web no responde por internet" "no responde por internet" URL_SALUD=https://x.test/health SALUD_FALLA=1

# ---- varios problemas a la vez: un solo aviso con todos ----
estado_sano; crear_copia "$T/copias" "40 hours ago"; echo "running unhealthy" > "$T/estado/gdb-crud-web-1"
caso_falla "varios problemas: un aviso con la lista" "backup diario" DISCO_PORCIENTO=95
if grep -q "gdb-crud-web-1" "$T/log.txt" && grep -q "disco esta al 95" "$T/log.txt" && [[ "$(grep -c 'STDIN:' "$T/log.txt")" == 1 ]]; then ok "un solo /fail con todos los problemas"; else falla "el aviso no reune todos los problemas"; fi
crear_copia "$T/copias" "1 hour ago"

# ---- el latido ----
estado_sano
if correr HEARTBEAT_URL_FILE="$T/no-existe"; then falla "sin direccion de latido debia fallar"; elif sin_ping && grep -q "ADVERTENCIA" "$T/salida.txt"; then ok "sin direccion de latido: advierte, falla y no llama a nadie"; else falla "sin direccion de latido mal manejado"; fi
if correr PING_FALLA=1; then falla "si el servicio del latido no responde debia fallar"; elif grep -q "no se pudo enviar el latido" "$T/salida.txt"; then ok "si el servicio del latido no responde: advierte y falla"; else falla "fallo del ping mal manejado"; fi

echo
if [[ "$fallos" -eq 0 ]]; then echo "Alertas: todas las pruebas pasaron."; exit 0; fi
echo "Alertas: $fallos prueba(s) fallaron."
exit 1
