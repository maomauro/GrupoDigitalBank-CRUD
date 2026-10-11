#!/bin/sh
# SOLO DESARROLLO: prepara el vhost de produccion y un certificado autofirmado, y arranca nginx (sin privilegios).
set -eu

CERT=/etc/nginx/ssl/gdb-crud.sitiosapps.com.pem
LLAVE=/etc/nginx/ssl/gdb-crud.sitiosapps.com.key

mkdir -p /etc/nginx/ssl
if [ ! -f "$CERT" ]; then
    openssl req -x509 -nodes -newkey rsa:2048 -days 30 -subj "/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,DNS:gdb-crud.sitiosapps.com" \
        -keyout "$LLAVE" -out "$CERT" 2>/dev/null
fi

# El MISMO vhost que va a produccion; en desarrollo se accede como "localhost", asi que el nombre se vuelve comodin
sed 's/server_name gdb-crud.sitiosapps.com;/server_name _;/' /vhost/gdb-crud.conf > /etc/nginx/conf.d/default.conf

nginx -t
exec nginx -g 'daemon off;'
