#!/bin/bash
# UNICA orden que puede ejecutar la llave SSH de GitHub Actions. Se instala con dueno root en /usr/local/sbin y se
# referencia desde authorized_keys:
#
#   restrict,command="/usr/local/sbin/gdb-crud-entrada.sh" ssh-ed25519 AAAA... gdb-crud-deploy
#
# `restrict` quita terminal, reenvio de puertos y de agente; `command=` ignora lo que el cliente pida y ejecuta ESTE
# script, que solo entiende "desplegar <tag>". Ver ADR 0011.
#
# Protocolo (entrada estandar):
#   linea 1      token de GITHUB_TOKEN para leer las imagenes de GHCR ("-" = no iniciar sesion, solo pruebas)
#   resto        paquete .tar.gz con docker-compose.yml, docker-compose.prod.yml, Caddyfile y db/
#
# El token vive solo mientras dura el despliegue: se usa en una carpeta de configuracion de Docker temporal que se
# borra al terminar (no queda ninguna credencial en el servidor).
set -euo pipefail

DIR=${GDB_DIR:-/srv/sitiosapps/gdb-crud}
SBIN=${GDB_SBIN:-/usr/local/sbin}
REGISTRO=${REGISTRO:-ghcr.io}
USUARIO_REGISTRO=maomauro

# ---- 1. La unica orden permitida ----
read -r -a orden <<< "${SSH_ORIGINAL_COMMAND:-}"
if [ "${#orden[@]}" -ne 2 ] || [ "${orden[0]}" != "desplegar" ]; then
    echo "Orden no permitida. Solo: desplegar <tag>" >&2
    exit 2
fi
tag=${orden[1]}
if ! [[ $tag =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]]; then
    echo "Tag no valido." >&2
    exit 2
fi

# ---- 2. Token y paquete ----
read -r token || { echo "Falta el token en la primera linea." >&2; exit 2; }
[ -n "$token" ] || { echo "Token vacio." >&2; exit 2; }

mkdir -p "$DIR/releases"
trabajo=$(mktemp -d)
entrega=$DIR/releases/.entrega-$$
trap 'rm -rf "$trabajo" "$entrega"' EXIT
export DOCKER_CONFIG=$trabajo/docker
mkdir -p "$DOCKER_CONFIG"
# Una carpeta de configuracion propia oculta los plugins instalados por usuario (por ejemplo `docker compose` en
# ~/.docker/cli-plugins); si existen, se copian para que sigan disponibles
if [ -d "${HOME:-/nonexistent}/.docker/cli-plugins" ]; then
    cp -r "$HOME/.docker/cli-plugins" "$DOCKER_CONFIG/cli-plugins"
fi

cat > "$trabajo/paquete.tgz"

# ---- 3. Revisar el paquete ANTES de extraerlo: solo archivos regulares y carpetas con nombres esperados ----
tipos=$(tar -tvzf "$trabajo/paquete.tgz" | cut -c1 | sort -u | tr -d '\n')
if [ -n "$tipos" ] && ! [[ $tipos =~ ^(-d|d-|-|d)$ ]]; then
    echo "El paquete contiene enlaces u otros tipos de archivo no permitidos." >&2
    exit 3
fi
permitido='^(\./)?(docker-compose\.yml|docker-compose\.prod\.yml|Caddyfile|db/?|db/[A-Za-z0-9_.-]+)$'
while IFS= read -r nombre; do
    if ! [[ $nombre =~ $permitido ]] || [[ $nombre == *..* ]]; then
        echo "El paquete contiene un archivo no permitido: $nombre" >&2
        exit 3
    fi
done < <(tar -tzf "$trabajo/paquete.tgz")

mkdir -p "$entrega"
umask 022
tar -xzf "$trabajo/paquete.tgz" -C "$entrega" --no-same-owner --no-same-permissions
for obligatorio in docker-compose.yml docker-compose.prod.yml Caddyfile db/migrate.sh; do
    [ -f "$entrega/$obligatorio" ] || { echo "Falta $obligatorio en el paquete." >&2; exit 3; }
done

# ---- 4. Iniciar sesion en el registro (si corresponde) y desplegar ----
if [ "$token" != "-" ]; then
    printf '%s' "$token" | docker login "$REGISTRO" -u "$USUARIO_REGISTRO" --password-stdin > /dev/null
fi
unset token

# Con "bash" explicito: no depende del bit de ejecucion ni de que la carpeta no este montada como noexec
bash "$SBIN/gdb-crud-desplegar.sh" "$tag" "$entrega"
