#!/bin/bash
# Prueba que la ultima copia de seguridad SE PUEDE RESTAURAR: la carga en un PostgreSQL desechable (nunca toca la base
# real) y comprueba que el esquema y las funciones estan. "Un backup sin restauracion probada no cuenta" (plan, 9.1).
#
#   gdb-crud-probar-restauracion.sh [archivo.sql.gz]     (sin argumento, usa el mas reciente)
#
# Variables: BACKUP_DIR. Sale con 0 si la restauracion es correcta.
set -euo pipefail

DESTINO=${BACKUP_DIR:-/srv/sitiosapps/_backups/gdb-crud}
archivo=${1:-$(find "$DESTINO" -maxdepth 1 -type f -name 'usuarios_*.sql.gz' -printf '%T@ %p\n' | sort -rn | head -n1 | cut -d' ' -f2-)}
if [ -z "$archivo" ] || [ ! -f "$archivo" ]; then
    echo "No hay copias de seguridad en $DESTINO" >&2
    exit 1
fi
echo "Restaurando $archivo"

# 1) La suma de verificacion, si existe, y la integridad del gzip
if [ -f "$archivo.sha256" ]; then
    ( cd "$(dirname "$archivo")" && sha256sum -c "$(basename "$archivo").sha256" )
fi
gzip -t "$archivo"

# 2) Restaurar en un contenedor desechable
contenedor=gdb-crud-restauracion-$$
trap 'docker rm -f "$contenedor" > /dev/null 2>&1 || true' EXIT
docker run -d --name "$contenedor" -e POSTGRES_PASSWORD=prueba -e POSTGRES_DB=restauracion postgres:17-alpine > /dev/null
for _ in $(seq 1 40); do
    docker exec "$contenedor" pg_isready -U postgres -d restauracion > /dev/null 2>&1 && break
    sleep 1
done
sleep 1   # la imagen reinicia el servidor una vez tras inicializar

# El volcado se hizo con --no-owner: los objetos quedan a nombre de postgres. Los GRANT hacia roles que aqui no
# existen producen avisos, no errores: la restauracion debe terminar bien igualmente.
gzip -dc "$archivo" | docker exec -i "$contenedor" psql -U postgres -d restauracion -v ON_ERROR_STOP=0 -q > /dev/null 2> "${TMPDIR:-/tmp}/$contenedor.err" || true

# 3) Comprobaciones: la tabla, las dos funciones y las filas
consulta() { docker exec "$contenedor" psql -U postgres -d restauracion -At -c "$1"; }
tabla=$(consulta "select count(*) from pg_class where relname='usuario' and relkind='r'")
funciones=$(consulta "select count(*) from pg_proc where proname in ('fn_usuario_crud','fn_usuario_pagina')")
migraciones=$(consulta "select count(*) from schema_migrations")
filas=$(consulta "select count(*) from usuario")
rm -f "${TMPDIR:-/tmp}/$contenedor.err"

echo "tabla usuario: $tabla | funciones: $funciones/2 | migraciones: $migraciones | usuarios: $filas"
if [ "$tabla" = 1 ] && [ "$funciones" = 2 ] && [ "$migraciones" -ge 1 ]; then
    echo "RESTAURACION CORRECTA"
else
    echo "LA RESTAURACION NO ES VALIDA" >&2
    exit 1
fi
