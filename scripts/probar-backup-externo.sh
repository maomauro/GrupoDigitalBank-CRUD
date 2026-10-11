#!/bin/bash
# Prueba deploy/backup-externo.sh (copia externa cifrada con age y rotada) sin tocar nada real: el "almacen remoto" es una
# carpeta local (rclone la trata como un destino mas), las llaves de age son de pruebas y todo vive en una carpeta temporal.
#
#   bash scripts/probar-backup-externo.sh        (en Windows: scripts\probar-backup-externo.ps1)
#
# Requiere bash, age, rclone y las herramientas GNU habituales. Codigo de salida: 0 si todo pasa.
set -uo pipefail

RAIZ=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
SCRIPT=$RAIZ/deploy/backup-externo.sh
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT
fallos=0

titulo() { printf '\n== %s ==\n' "$1"; }
ok() { printf 'OK     %s\n' "$1"; }
falla() { printf 'FALLA  %s %s\n' "$1" "${2:-}"; fallos=$((fallos + 1)); }
chequear() {
    local nombre=$1
    shift
    if "$@" > /dev/null 2>&1; then ok "$nombre"; else falla "$nombre"; fi
}
igual_a() {
    if [[ "$3" == "$2" ]]; then ok "$1 ($3)"; else falla "$1" "-> '$3', esperado '$2'"; fi
}

for herramienta in age age-keygen rclone; do
    command -v "$herramienta" > /dev/null || { echo "Falta $herramienta" >&2; exit 2; }
done

# ---- preparacion: llaves, copias locales y destino remoto ----
age-keygen -o "$T/clave.txt" 2> /dev/null
age-keygen -y "$T/clave.txt" > "$T/destinatario.txt"
age-keygen -o "$T/clave-ajena.txt" 2> /dev/null
mkdir -p "$T/proyecto" "$T/tmp" "$T/remoto"
echo "POSTGRES_PASSWORD=SECRETO_DE_PRUEBA" > "$T/proyecto/.env"
REMOTO=$T/remoto/gdb-crud

crear_copia() { # crear_copia <carpeta> <n>: una copia valida con su suma; el n mayor es el mas reciente
    local carpeta=$1 n=$2 archivo
    mkdir -p "$carpeta"
    archivo=$carpeta/usuarios_2026-10-11_00000$n.sql.gz
    printf 'CREATE TABLE usuario (id int); -- COPIA-%s\n' "$n" | gzip > "$archivo"
    (cd "$carpeta" && sha256sum "$(basename "$archivo")" > "$(basename "$archivo").sha256")
    touch -d "@$(($(date +%s) - 100 + n))" "$archivo"
}

correr() { # correr <ajustes de entorno...>: ejecuta el script con el entorno de pruebas
    env RCLONE_REMOTE="$REMOTO" BACKUP_DIR="$T/copias" GDB_DIR="$T/proyecto" AGE_RECIPIENT_FILE="$T/destinatario.txt" \
        TMPDIR="$T/tmp" "$@" bash "$SCRIPT" > "$T/salida.txt" 2>&1
}
cuenta() { find "$REMOTO" -maxdepth 1 -type f -name "$1" 2> /dev/null | wc -l | tr -d ' '; }
mas_nuevo() { find "$REMOTO" -maxdepth 1 -type f -name "$1" -printf '%f\n' | sort | tail -n1; }

# ---- 1. primera copia ----
titulo "1. Primera copia externa"
crear_copia "$T/copias" 1
correr
igual_a "termina bien" 0 "$?"
igual_a "sube 1 copia de la base cifrada" 1 "$(cuenta 'usuarios_*.sql.gz.age')"
igual_a "sube 1 copia del .env cifrada" 1 "$(cuenta 'env_*.age')"
chequear "lo subido NO contiene texto legible de la base" bash -c "! grep -rqa 'CREATE TABLE' '$REMOTO'"
chequear "lo subido NO contiene el secreto del .env en claro" bash -c "! grep -rqa 'SECRETO_DE_PRUEBA' '$REMOTO'"
copia_remota=$REMOTO/$(mas_nuevo 'usuarios_*.sql.gz.age')
env_remoto=$REMOTO/$(mas_nuevo 'env_*.age')
chequear "con la llave privada la base se recupera identica" bash -c "age -d -i '$T/clave.txt' '$copia_remota' | gzip -dc | grep -q 'COPIA-1'"
chequear "con la llave privada el .env se recupera identico" bash -c "age -d -i '$T/clave.txt' '$env_remoto' | cmp -s - '$T/proyecto/.env'"
chequear "con OTRA llave NO se puede descifrar" bash -c "! age -d -i '$T/clave-ajena.txt' '$copia_remota'"
igual_a "no quedan temporales sin borrar" "" "$(ls -A "$T/tmp")"

# ---- 2. rotacion ----
titulo "2. Rotacion: solo las 2 ultimas"
for n in 2 3 4; do
    crear_copia "$T/copias" "$n"
    sleep 1 # el nombre del .env lleva la hora con segundos
    correr
    igual_a "ejecucion con la copia $n termina bien" 0 "$?"
done
igual_a "quedan exactamente 2 copias de la base" 2 "$(cuenta 'usuarios_*.sql.gz.age')"
igual_a "quedan exactamente 2 copias del .env" 2 "$(cuenta 'env_*.age')"
chequear "se conservo la mas nueva (4) y se borro la mas vieja (1)" bash -c "[ -f '$REMOTO/usuarios_2026-10-11_000004.sql.gz.age' ] && [ ! -f '$REMOTO/usuarios_2026-10-11_000001.sql.gz.age' ]"
chequear "se conservo la 3 y se borro la 2" bash -c "[ -f '$REMOTO/usuarios_2026-10-11_000003.sql.gz.age' ] && [ ! -f '$REMOTO/usuarios_2026-10-11_000002.sql.gz.age' ]"
chequear "el script informa de lo que borra" grep -q "Borrada la copia externa antigua" "$T/salida.txt"
sleep 1
correr COPIAS_EXTERNAS=1
igual_a "con COPIAS_EXTERNAS=1 queda una sola" 1 "$(cuenta 'usuarios_*.sql.gz.age')"

# ---- 3. si la verificacion de lo subido falla, no se borra nada ----
titulo "3. Verificacion de lo subido"
crear_copia "$T/copias" 5
sleep 1
correr
igual_a "con una sola copia remota y una nueva quedan 2" 2 "$(cuenta 'usuarios_*.sql.gz.age')"
crear_copia "$T/copias" 6
mkdir -p "$T/shim"
real=$(command -v rclone)
# shellcheck disable=SC2016
printf '#!/bin/bash
[[ "${1:-}" == check ]] && exit 1
exec %s "$@"
' "$real" > "$T/shim/rclone"
chmod +x "$T/shim/rclone"
sleep 1
correr PATH="$T/shim:$PATH"
igual_a "si 'rclone check' falla, termina con error" 1 "$?"
chequear "y avisa de que no se rota" grep -q "no se rota nada" "$T/salida.txt"
igual_a "y NO se borro ninguna copia anterior (con rotacion habrian quedado 2)" 3 "$(cuenta 'usuarios_*.sql.gz.age')"
sleep 1
correr
igual_a "sin el fallo, rota y vuelve a dejar 2" 2 "$(cuenta 'usuarios_*.sql.gz.age')"

# ---- 4. casos que deben fallar sin subir nada ----
titulo "4. Casos de fallo: no se sube nada"
antes=$(find "$REMOTO" -type f | wc -l | tr -d ' ')
sin_cambios() { igual_a "$1: no se sube nada" "$antes" "$(find "$REMOTO" -type f | wc -l | tr -d ' ')"; }

mkdir -p "$T/vacio"
correr BACKUP_DIR="$T/vacio"
igual_a "sin copias locales falla" 1 "$?"
sin_cambios "sin copias locales"

crear_copia "$T/viejo" 1
touch -d '10 days ago' "$T/viejo/usuarios_2026-10-11_000001.sql.gz"
correr BACKUP_DIR="$T/viejo"
igual_a "una copia local de hace 10 dias falla" 1 "$?"
chequear "y explica que el backup diario esta fallando" grep -q "esta fallando" "$T/salida.txt"
sin_cambios "copia local vieja"

crear_copia "$T/corrupta" 1
echo "0000000000000000000000000000000000000000000000000000000000000000  usuarios_2026-10-11_000001.sql.gz" > "$T/corrupta/usuarios_2026-10-11_000001.sql.gz.sha256"
correr BACKUP_DIR="$T/corrupta"
igual_a "una suma de verificacion que no coincide falla" 1 "$?"
sin_cambios "suma incorrecta"

mkdir -p "$T/danada"
echo "esto no es un gzip" > "$T/danada/usuarios_2026-10-11_000001.sql.gz"
correr BACKUP_DIR="$T/danada"
igual_a "un gzip danado falla" 1 "$?"
sin_cambios "gzip danado"

correr AGE_RECIPIENT_FILE="$T/no-existe.txt"
igual_a "sin la llave publica falla" 1 "$?"
sin_cambios "sin llave publica"

echo "esto no es una llave" > "$T/basura.txt"
correr AGE_RECIPIENT_FILE="$T/basura.txt"
igual_a "una llave publica invalida falla" 1 "$?"
sin_cambios "llave publica invalida"

env -u RCLONE_REMOTE BACKUP_DIR="$T/copias" GDB_DIR="$T/proyecto" AGE_RECIPIENT_FILE="$T/destinatario.txt" TMPDIR="$T/tmp" bash "$SCRIPT" > "$T/salida.txt" 2>&1
igual_a "sin RCLONE_REMOTE falla" 1 "$?"

correr COPIAS_EXTERNAS=0
igual_a "COPIAS_EXTERNAS=0 falla (no se puede borrar todo)" 1 "$?"
sin_cambios "COPIAS_EXTERNAS=0"
igual_a "tras los fallos no quedan temporales" "" "$(ls -A "$T/tmp")"

# ---- 5. sin .env ----
titulo "5. Sin .env: sube solo la base y avisa"
mkdir -p "$T/sin-env"
sleep 1
correr GDB_DIR="$T/sin-env"
igual_a "termina bien" 0 "$?"
chequear "avisa de que no hay .env" grep -q "ADVERTENCIA" "$T/salida.txt"

echo
if [[ "$fallos" -eq 0 ]]; then echo "Copia externa: todas las pruebas pasaron."; exit 0; fi
echo "Copia externa: $fallos prueba(s) fallaron."
exit 1
