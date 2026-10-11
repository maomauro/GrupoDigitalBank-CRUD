#!/bin/bash
# Prueba de punta a punta del mecanismo de despliegue (deploy/*.sh, docker-compose.prod.yml, ADR 0011) contra un
# "servidor" simulado en Docker: registro local, red compartida, nginx con el vhost de produccion delante.
#
#   bash scripts/probar-despliegue.sh
#
# Requiere Linux (o un contenedor Linux con el socket de Docker), docker, docker compose, python3 y bash. Lo ejecuta el
# CI; en Windows se corre dentro de un contenedor (ver docs/plan-de-trabajo.md, fase 7). No toca ningun recurso real:
# todo lleva el prefijo "gdbprueba" y se elimina al terminar.
#
# Cubre: ordenes no permitidas, paquetes con enlaces o rutas peligrosas, composes maliciosos (la politica de
# seguridad), despliegue correcto, que nada publique puertos, que solo el proxy entre a la red compartida, un segundo
# despliegue con copia previa, un despliegue roto que vuelve a la version anterior, un bloqueo concurrente, una
# descarga fallida, y la copia de seguridad con su restauracion.
# Las funciones auxiliares se invocan de forma indirecta (trap, chequear, esperar_codigo). Segun la version de shellcheck
# el aviso es SC2317 ("inalcanzable") o SC2329 ("nunca invocada"): se silencian los dos
# shellcheck disable=SC2317,SC2329
set -uo pipefail

RAIZ=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PRUEBA=${GDB_PRUEBA_DIR:-$(mktemp -d)}
RED=gdbprueba-red
PROYECTO=gdbprueba
REGISTRO_LOCAL=localhost:5000
REPO=maomauro/grupodigitalbank-crud
CLAVE='ClaveDePrueba-123456'
IMG=$REGISTRO_LOCAL/$REPO
fallos=0
export MSYS_NO_PATHCONV=1

export GDB_DIR=$PRUEBA/proyecto GDB_SBIN=$PRUEBA/sbin GDB_PROYECTO=$PROYECTO
export REGISTRO=$REGISTRO_LOCAL RED_COMPARTIDA=$RED BACKUP_DIR=$PRUEBA/backups

titulo() { printf '\n== %s ==\n' "$1"; }
ok() { printf 'OK     %s\n' "$1"; }
falla() { printf 'FALLA  %s %s\n' "$1" "${2:-}"; fallos=$((fallos + 1)); }
# chequear "descripcion" comando...   (el comando se ejecuta; 0 = cumple)
chequear() {
    local nombre=$1
    shift
    if "$@" > /dev/null 2>&1; then ok "$nombre"; else falla "$nombre"; fi
}
# esperar_codigo "descripcion" codigo-esperado comando...
esperar_codigo() {
    local nombre=$1 esperado=$2 real
    shift 2
    "$@" > "$PRUEBA/ultima-salida.txt" 2>&1
    real=$?
    if [ "$real" = "$esperado" ]; then ok "$nombre (codigo $real)"; else falla "$nombre" "-> codigo $real, esperado $esperado: $(tail -n2 "$PRUEBA/ultima-salida.txt" | tr '\n' ' ')"; fi
}

limpiar() {
    docker rm -f gdbprueba-registro gdbprueba-borde > /dev/null 2>&1
    docker ps -aq --filter "label=com.docker.compose.project=$PROYECTO" | xargs -r docker rm -f > /dev/null 2>&1
    docker volume ls -q --filter "label=com.docker.compose.project=$PROYECTO" | xargs -r docker volume rm > /dev/null 2>&1
    docker network rm "$RED" > /dev/null 2>&1
    docker network ls -q --filter "label=com.docker.compose.project=$PROYECTO" | xargs -r docker network rm > /dev/null 2>&1
    for v in v1 v2 v3; do docker rmi "$IMG/negocios:$v" "$IMG/web:$v" > /dev/null 2>&1; done
    docker rmi gdbprueba-borde > /dev/null 2>&1
    [ -z "${GDB_PRUEBA_DIR:-}" ] && rm -rf "$PRUEBA"
    return 0
}
trap limpiar EXIT

for herramienta in docker python3; do
    command -v "$herramienta" > /dev/null || { echo "Falta $herramienta" >&2; exit 2; }
done
limpiar_previo() { docker rm -f gdbprueba-registro gdbprueba-borde > /dev/null 2>&1; docker network rm "$RED" > /dev/null 2>&1; true; }
limpiar_previo

mkdir -p "$PRUEBA"
# ----------------------------------------------------------------------------------------------------------------
titulo "Preparacion: red compartida, registro local, imagenes y scripts instalados"
docker network create "$RED" > /dev/null || { echo "No se pudo crear la red" >&2; exit 2; }
docker run -d --name gdbprueba-registro -p 127.0.0.1:5000:5000 registry:2 > /dev/null || { echo "No se pudo iniciar el registro" >&2; exit 2; }
docker build -q -f "$RAIZ/src/GrupoDigitalBank.Negocios/Dockerfile" -t "$IMG/negocios:v1" "$RAIZ" > /dev/null || exit 2
docker build -q -f "$RAIZ/src/GrupoDigitalBank.Presentacion/Dockerfile" -t "$IMG/web:v1" "$RAIZ" > /dev/null || exit 2
docker tag "$IMG/negocios:v1" "$IMG/negocios:v2"
docker tag "$IMG/web:v1" "$IMG/web:v2"
# v3: una version ROTA de la web (el contenedor muere al arrancar)
printf 'FROM %s/web:v2\nENTRYPOINT ["false"]\n' "$IMG" | docker build -q -t "$IMG/web:v3" - > /dev/null || exit 2
docker tag "$IMG/negocios:v1" "$IMG/negocios:v3"
for img in negocios:v1 web:v1 negocios:v2 web:v2 negocios:v3 web:v3; do docker push -q "$IMG/$img" > /dev/null || { echo "No se pudo subir $img" >&2; exit 2; }; done

bash "$RAIZ/deploy/instalar.sh" --destino "$GDB_SBIN" > /dev/null || exit 2
mkdir -p "$GDB_DIR" "$BACKUP_DIR"
hash=$(docker run --rm caddy:2-alpine caddy hash-password --plaintext "$CLAVE" | tr -d '\r\n')
cat > "$GDB_DIR/.env" << EOF
DOMINIO=gdb-crud.sitiosapps.com
POSTGRES_DB=usuarios_db
POSTGRES_USER=usuarios_admin
POSTGRES_PASSWORD=pg_prueba_$$
APP_DB_USER=usuarios_app
APP_DB_PASSWORD=app_prueba_$$
AUTH_USUARIO=admin
AUTH_HASH='$hash'
IMAGE_TAG=latest
GITHUB_REPOSITORY=$REPO
EOF
chmod 600 "$GDB_DIR/.env"

# Paquete bueno: lo mismo que arma el workflow de despliegue
cd "$RAIZ" || exit 2
tar -czf "$PRUEBA/paquete-bueno.tgz" docker-compose.yml docker-compose.prod.yml Caddyfile db

entrada() { # entrada "<orden>" <paquete> [token]
    local orden=$1 paquete=$2 token=${3:--}
    { printf '%s\n' "$token"; cat "$paquete"; } | SSH_ORIGINAL_COMMAND="$orden" bash "$GDB_SBIN/gdb-crud-entrada.sh"
}
sin_release() { [ ! -f "$GDB_DIR/releases/ACTUAL" ]; }
contenedores_del_proyecto() { docker ps -aq --filter "label=com.docker.compose.project=$PROYECTO" | wc -l | tr -d ' '; }

# ----------------------------------------------------------------------------------------------------------------
titulo "1. La llave solo puede pedir 'desplegar <tag>'"
# Incluye ordenes de DOS palabras con un tag valido ("otro v1"): sin la comprobacion de la primera palabra pasarian
# la de las dos palabras y la del tag, y desplegarian
for orden in "" "ls" "desplegar" "desplegar v1 v2" "desplegar ../../etc" 'desplegar v1;rm' "desplegar -rf" "bash" "cat /etc/passwd"     "otro v1" "DESPLEGAR v1" "desplegar2 v1" "rm v1"; do
    esperar_codigo "orden rechazada: [$orden]" 2 entrada "$orden" "$PRUEBA/paquete-bueno.tgz"
done
chequear "ninguna orden rechazada dejo una version instalada" sin_release

# ----------------------------------------------------------------------------------------------------------------
titulo "2. Paquetes con archivos peligrosos se rechazan ANTES de extraerlos"
python3 - "$PRUEBA" << 'PY'
import io, os, sys, tarfile
d = sys.argv[1]
def paquete(nombre, extras):
    with tarfile.open(os.path.join(d, nombre), "w:gz") as t:
        for archivo in ("docker-compose.yml", "docker-compose.prod.yml", "Caddyfile"):
            t.add(archivo)
        t.add("db")
        extras(t)
def enlace(t):
    i = tarfile.TarInfo("db/enlace"); i.type = tarfile.SYMTYPE; i.linkname = "/etc/passwd"; t.addfile(i)
def escape(t):
    i = tarfile.TarInfo("../fuera.txt"); i.size = 1; t.addfile(i, io.BytesIO(b"x"))
def absoluto(t):
    i = tarfile.TarInfo("/tmp/absoluto.txt"); i.size = 1; t.addfile(i, io.BytesIO(b"x"))
def extra(t):
    i = tarfile.TarInfo("evil.sh"); i.size = 1; t.addfile(i, io.BytesIO(b"x"))
def profundo(t):
    i = tarfile.TarInfo("db/sub/otro.sql"); i.size = 1; t.addfile(i, io.BytesIO(b"x"))
for nombre, f in (("enlace", enlace), ("escape", escape), ("absoluto", absoluto), ("extra", extra), ("profundo", profundo)):
    paquete(f"malo-{nombre}.tgz", f)
with tarfile.open(os.path.join(d, "malo-incompleto.tgz"), "w:gz") as t:
    t.add("docker-compose.yml")
PY
for malo in enlace escape absoluto extra profundo incompleto; do
    esperar_codigo "paquete rechazado: $malo" 3 entrada "desplegar v1" "$PRUEBA/malo-$malo.tgz"
done
chequear "ningun contenedor levantado" [ "$(contenedores_del_proyecto)" = 0 ]
chequear "sigue sin haber version instalada" sin_release

# ----------------------------------------------------------------------------------------------------------------
titulo "3. La politica de seguridad rechaza composes maliciosos (un compose arbitrario seria root en el servidor)"
python3 - "$PRUEBA" "$RAIZ" << 'PY'
import os, shutil, sys, tarfile
d, raiz = sys.argv[1], sys.argv[2]
def leer(n): return open(os.path.join(raiz, n), encoding="utf-8").read()
base, prod = leer("docker-compose.yml"), leer("docker-compose.prod.yml")
def cambiar(texto, a, b):
    assert a in texto, a
    return texto.replace(a, b, 1)
variantes = {
    "privilegiado": (cambiar(base, "    restart: unless-stopped\n", "    restart: unless-stopped\n    privileged: true\n"), prod),
    "monta-raiz": (cambiar(base, "- ./db:/migraciones:ro", "- /:/host"), prod),
    "monta-docker-sock": (cambiar(base, "- ./db:/migraciones:ro", "- /var/run/docker.sock:/var/run/docker.sock"), prod),
    "publica-puertos": (cambiar(base, 'expose:\n      - "8080"', 'ports:\n      - "8081:8080"'), prod),
    "red-del-host": (cambiar(base, "    restart: unless-stopped\n", "    restart: unless-stopped\n    network_mode: host\n"), prod),
    "otro-registro": (base, cambiar(prod, "${REGISTRO:-ghcr.io}/${GITHUB_REPOSITORY:?Define GITHUB_REPOSITORY en .env}/negocios", "evil.example.com/atacante/negocios")),
    "alias-ajeno": (base, cambiar(prod, "aliases: [gdb-crud-web]", "aliases: [gdb-crud-web, mariadb]")),
    "web-en-la-red-compartida": (base, cambiar(prod, "  web:\n    image:", "  web:\n    networks:\n      default: {}\n      compartida: {}\n    image:")),
    "servicio-extra": (base + "\n  intruso:\n    image: alpine\n", prod),
    "volumen-ajeno": (cambiar(base, "- dpkeys:/keys", "- datos_de_otro:/keys"), prod),
    "build-en-produccion": (cambiar(base, "    restart: unless-stopped\n", "    restart: unless-stopped\n    build: .\n"), prod),
    "linux-capabilities": (cambiar(base, "    restart: unless-stopped\n", "    restart: unless-stopped\n    cap_add: [SYS_ADMIN]\n"), prod),
}
for nombre, (b, p) in variantes.items():
    carpeta = os.path.join(d, "var-" + nombre)
    shutil.rmtree(carpeta, ignore_errors=True)
    os.makedirs(carpeta)
    open(os.path.join(carpeta, "docker-compose.yml"), "w", encoding="utf-8", newline="\n").write(b)
    open(os.path.join(carpeta, "docker-compose.prod.yml"), "w", encoding="utf-8", newline="\n").write(p)
    shutil.copy(os.path.join(raiz, "Caddyfile"), carpeta)
    shutil.copytree(os.path.join(raiz, "db"), os.path.join(carpeta, "db"))
    with tarfile.open(os.path.join(d, f"var-{nombre}.tgz"), "w:gz") as t:
        for a in ("docker-compose.yml", "docker-compose.prod.yml", "Caddyfile", "db"):
            t.add(os.path.join(carpeta, a), arcname=a)
print(" ".join(sorted(variantes)))
PY
for variante in privilegiado monta-raiz monta-docker-sock publica-puertos red-del-host otro-registro alias-ajeno web-en-la-red-compartida servicio-extra volumen-ajeno build-en-produccion linux-capabilities; do
    esperar_codigo "compose rechazado: $variante" 3 entrada "desplegar v1" "$PRUEBA/var-$variante.tgz"
done
chequear "ningun compose malicioso llego a levantar un contenedor" [ "$(contenedores_del_proyecto)" = 0 ]
chequear "sigue sin haber version instalada" sin_release

# ----------------------------------------------------------------------------------------------------------------
titulo "4. Primer despliegue (v1), con el token entregado por la entrada estandar"
config_antes=$(sha256sum "${HOME:-/root}/.docker/config.json" 2> /dev/null | cut -d' ' -f1)
esperar_codigo "desplegar v1 termina bien" 0 entrada "desplegar v1" "$PRUEBA/paquete-bueno.tgz" "tokenfalso"
chequear "ACTUAL = v1" bash -c "[ \"\$(cat '$GDB_DIR/releases/ACTUAL')\" = v1 ]"
chequear "los 5 servicios del proyecto estan en marcha o terminaron (migrador)" bash -c "[ \"\$(docker ps -aq --filter label=com.docker.compose.project=$PROYECTO | wc -l)\" = 5 ]"
sanos() { [ "$(docker ps --filter "label=com.docker.compose.project=$PROYECTO" --filter health=healthy -q | wc -l | tr -d ' ')" = 4 ]; }
chequear "db, negocios, web y proxy estan sanos" sanos
sin_puertos() { ! docker ps --filter "label=com.docker.compose.project=$PROYECTO" --format '{{.Ports}}' | grep -q -- '->'; }
chequear "NINGUN contenedor publica puertos en el host" sin_puertos
solo_el_proxy_en_la_red() {
    local miembros
    miembros=$(docker network inspect "$RED" -f '{{range .Containers}}{{.Name}} {{end}}')
    [[ $miembros == *"$PROYECTO-proxy-1"* ]] && ! [[ $miembros =~ $PROYECTO-(db|negocios|web|migrador)-1 ]]
}
chequear "solo el proxy esta en la red compartida (db, negocios y web no)" solo_el_proxy_en_la_red
alias_del_proxy() { docker inspect -f '{{json .NetworkSettings.Networks}}' "$PROYECTO-proxy-1" | grep -q gdb-crud-web; }
chequear "el proxy tiene el alias gdb-crud-web en la red compartida" alias_del_proxy
chequear "el token no dejo credenciales en el servidor (config.json de Docker intacto)" bash -c "[ \"\$(sha256sum '${HOME:-/root}/.docker/config.json' 2>/dev/null | cut -d' ' -f1)\" = '$config_antes' ]"
chequear "los scripts del servidor no quedaron escribibles por el proyecto" bash -c "[ -z \"\$(find '$GDB_DIR' -maxdepth 1 -name '*.sh')\" ]"

# nginx de verdad delante, con el MISMO vhost de produccion, en la red compartida
mkdir -p "$PRUEBA/vhost"
cp "$RAIZ/deploy/nginx/gdb-crud.conf" "$PRUEBA/vhost/"
docker build -q -t gdbprueba-borde "$RAIZ/docker/dev/borde" > /dev/null
docker run -d --name gdbprueba-borde --network "$RED" -v "$PRUEBA/vhost/gdb-crud.conf:/vhost/gdb-crud.conf:ro" gdbprueba-borde > /dev/null
curl_red() { docker run --rm --network "$RED" curlimages/curl:latest -sk -H "Host: gdb-crud.sitiosapps.com" "$@"; }
esperar_borde() { for _ in $(seq 1 15); do [ "$(curl_red -o /dev/null -w '%{http_code}' https://gdbprueba-borde/health 2> /dev/null)" = 200 ] && return 0; sleep 2; done; return 1; }
chequear "a traves del nginx: /health responde 200" esperar_borde
codigo() { curl_red -o /dev/null -w '%{http_code}' "$@" 2> /dev/null; }
chequear "a traves del nginx: / sin credenciales = 401" [ "$(codigo https://gdbprueba-borde/)" = 401 ]
chequear "a traves del nginx: / con credenciales = 200" [ "$(codigo -u "admin:$CLAVE" https://gdbprueba-borde/)" = 200 ]
# Sin el envoltorio curl_red: este anade su propia cabecera Host y curl usa la primera que recibe
host_ajeno() {
    local codigo
    codigo=$(docker run --rm --network "$RED" curlimages/curl:latest -sk -o /dev/null -w '%{http_code}'         -H 'Host: otro.example.com' -u "admin:$CLAVE" https://gdbprueba-borde/ 2> /dev/null)
    [ "$codigo" = 404 ]
}
chequear "un nombre de host ajeno NO recibe la aplicacion (404 del comodin de Caddy, ni con credenciales)" host_ajeno

# ----------------------------------------------------------------------------------------------------------------
titulo "5. Segundo despliegue (v2): copia de seguridad previa y la version anterior se conserva"
docker exec "${PROYECTO}-db-1" psql -U usuarios_admin -d usuarios_db -q -At -c "select fn_usuario_crud('C', null, 'Persona de Prueba', date '1990-01-01', 'F')" > /dev/null
esperar_codigo "desplegar v2 termina bien" 0 entrada "desplegar v2" "$PRUEBA/paquete-bueno.tgz"
chequear "ACTUAL = v2" bash -c "[ \"\$(cat '$GDB_DIR/releases/ACTUAL')\" = v2 ]"
chequear "se hizo una copia de seguridad ANTES de desplegar" bash -c "ls '$BACKUP_DIR'/usuarios_*.sql.gz"
chequear "la copia lleva su suma de verificacion" bash -c "ls '$BACKUP_DIR'/usuarios_*.sql.gz.sha256"
chequear "la version v1 sigue disponible para volver" test -d "$GDB_DIR/releases/v1"
chequear "la web corre con la imagen v2" bash -c "docker inspect -f '{{.Config.Image}}' ${PROYECTO}-web-1 | grep -q ':v2$'"
chequear "tras el despliegue, el sitio sigue respondiendo a traves del nginx" esperar_borde

# ----------------------------------------------------------------------------------------------------------------
titulo "6. Despliegue ROTO (v3): vuelve solo a v2"
esperar_codigo "desplegar v3 falla" 1 entrada "desplegar v3" "$PRUEBA/paquete-bueno.tgz"
chequear "el script informa que el rollback fue correcto" grep -q "Rollback correcto" "$PRUEBA/ultima-salida.txt"
chequear "ACTUAL sigue siendo v2" bash -c "[ \"\$(cat '$GDB_DIR/releases/ACTUAL')\" = v2 ]"
chequear "la web vuelve a correr la imagen v2" bash -c "docker inspect -f '{{.Config.Image}}' ${PROYECTO}-web-1 | grep -q ':v2$'"
chequear "el sitio responde despues del rollback" esperar_borde

# ----------------------------------------------------------------------------------------------------------------
titulo "7. Casos de borde"
mkdir -p "$GDB_DIR/releases/.bloqueo"
esperar_codigo "un despliegue concurrente se rechaza" 1 entrada "desplegar v2" "$PRUEBA/paquete-bueno.tgz"
rmdir "$GDB_DIR/releases/.bloqueo"
esperar_codigo "una version cuyas imagenes no existen falla sin tocar nada" 1 entrada "desplegar v9" "$PRUEBA/paquete-bueno.tgz"
chequear "ACTUAL sigue siendo v2 y no quedo la carpeta de v9" bash -c "[ \"\$(cat '$GDB_DIR/releases/ACTUAL')\" = v2 ] && [ ! -d '$GDB_DIR/releases/v9' ]"
chequear "el sitio sigue respondiendo" esperar_borde
chequear "no quedaron carpetas de entrega a medias" [ -z "$(find "$GDB_DIR/releases" -maxdepth 1 -name '.entrega-*')" ]

# ----------------------------------------------------------------------------------------------------------------
titulo "8. Copia de seguridad y restauracion probada"
esperar_codigo "backup manual termina bien" 0 bash "$GDB_SBIN/gdb-crud-backup.sh"
ultimo=$(find "$BACKUP_DIR" -maxdepth 1 -name 'usuarios_*.sql.gz' -printf '%T@ %p\n' | sort -rn | head -n1 | cut -d' ' -f2-)
chequear "la suma de verificacion coincide" bash -c "cd '$BACKUP_DIR' && sha256sum -c '$(basename "$ultimo").sha256'"
chequear "los volcados solo los lee su dueno (600)" bash -c "[ \"\$(stat -c %a '$ultimo')\" = 600 ]"
esperar_codigo "la restauracion de la ultima copia es valida" 0 bash "$GDB_SBIN/gdb-crud-probar-restauracion.sh"
chequear "la copia restaurada contiene el usuario de prueba" grep -q "usuarios: 1" "$PRUEBA/ultima-salida.txt"
head -c 200 "$ultimo" > "$PRUEBA/corrupta.sql.gz"
esperar_codigo "una copia corrupta NO pasa la prueba de restauracion" 1 bash "$GDB_SBIN/gdb-crud-probar-restauracion.sh" "$PRUEBA/corrupta.sql.gz"
# Copias VALIDAS como gzip pero incompletas: la restauracion no puede darlas por buenas (el ultimo caso es el que ejercita
# la comprobacion final: las tablas existen y faltan las funciones, asi que ninguna consulta falla antes)
printf '%s
' 'SELECT 1;' | gzip > "$PRUEBA/sin-esquema.sql.gz"
esperar_codigo "una copia valida pero SIN el esquema NO pasa la prueba de restauracion" 1 bash "$GDB_SBIN/gdb-crud-probar-restauracion.sh" "$PRUEBA/sin-esquema.sql.gz"
printf '%s
' 'CREATE TABLE public.usuario (id integer);' 'CREATE TABLE public.schema_migrations (version text);' "INSERT INTO public.schema_migrations VALUES ('001');" | gzip > "$PRUEBA/sin-funciones.sql.gz"
esperar_codigo "una copia con las tablas pero SIN las funciones NO pasa la prueba de restauracion" 1 bash "$GDB_SBIN/gdb-crud-probar-restauracion.sh" "$PRUEBA/sin-funciones.sql.gz"
mkdir -p "$PRUEBA/vacio"
esperar_codigo "sin copias de seguridad la prueba de restauracion falla" 1 env BACKUP_DIR="$PRUEBA/vacio" bash "$GDB_SBIN/gdb-crud-probar-restauracion.sh"

echo
if [ "$fallos" -eq 0 ]; then echo "Despliegue: todas las pruebas pasaron."; exit 0; fi
echo "Despliegue: $fallos prueba(s) fallaron."
exit 1
