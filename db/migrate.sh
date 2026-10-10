#!/bin/sh
# Migrador de esquema (hallazgo H5). Se ejecuta en el servicio "migrador" de Docker Compose
# (imagen postgres:alpine) con db/ montada en /migraciones.
#
# Reglas:
#  - Aplica en orden alfabetico los scripts NNN_nombre.sql que aun no esten en schema_migrations.
#  - Cada script corre en UNA transaccion junto con su registro: si falla, no queda nada a medias.
#  - Guarda el checksum de cada script aplicado. Si un script ya aplicado cambia, se ABORTA:
#    los scripts aplicados nunca se editan; todo cambio es un script nuevo.
#  - Conexion por variables PG* (PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE).
#  - Variables de psql para los scripts: app_pass (contrasena del rol de aplicacion, ver H4).

set -eu

DIR="${MIGRACIONES_DIR:-/migraciones}"

# Sin avisos informativos de PostgreSQL ("ya existe, se omite"); los errores si se muestran
export PGOPTIONS="${PGOPTIONS:-} -c client_min_messages=warning"

psql_q() { psql -X -q -v ON_ERROR_STOP=1 -At "$@"; }

echo "migrador: conectando a ${PGHOST:-localhost}/${PGDATABASE:-}..."
psql_q -c "CREATE TABLE IF NOT EXISTS schema_migrations (
               version     text        PRIMARY KEY,
               checksum    text        NOT NULL,
               aplicada_en timestamptz NOT NULL DEFAULT now()
           )"

aplicadas=0
omitidas=0

for archivo in "$DIR"/[0-9][0-9][0-9]_*.sql; do
    [ -e "$archivo" ] || { echo "migrador: no hay scripts NNN_*.sql en $DIR" >&2; exit 1; }

    version=$(basename "$archivo")
    case "$version" in
        *[!A-Za-z0-9._-]*) echo "migrador: nombre de script no valido: $version" >&2; exit 1 ;;
    esac

    checksum=$(sha256sum "$archivo" | cut -d' ' -f1)
    registrado=$(psql_q -c "SELECT checksum FROM schema_migrations WHERE version = '$version'")

    if [ -n "$registrado" ]; then
        if [ "$registrado" != "$checksum" ]; then
            echo "migrador: ERROR el script ya aplicado '$version' fue modificado." >&2
            echo "          Los scripts aplicados no se editan: crea uno nuevo (NNN_...sql)." >&2
            exit 1
        fi
        echo "migrador: $version ya aplicado"
        omitidas=$((omitidas + 1))
        continue
    fi

    echo "migrador: aplicando $version"
    psql -X -q -v ON_ERROR_STOP=1 --single-transaction \
         -v "app_pass=${APP_DB_PASSWORD:-}" \
         -f "$archivo" \
         -c "INSERT INTO schema_migrations (version, checksum) VALUES ('$version', '$checksum')"
    aplicadas=$((aplicadas + 1))
done

echo "migrador: listo ($aplicadas aplicadas, $omitidas ya aplicadas)"
