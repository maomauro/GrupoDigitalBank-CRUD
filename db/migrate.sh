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
#  - Variables de psql para los scripts: app_user y app_pass (rol de aplicacion, ver H4), tomadas de
#    APP_DB_USER y APP_DB_PASSWORD. Son obligatorias: sin ellas no se crearia un rol utilizable.

set -eu
echo $0

DIR="${MIGRACIONES_DIR:-/migraciones}"

: "${APP_DB_USER:?APP_DB_USER es obligatoria (nombre del rol de aplicacion)}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD es obligatoria (contrasena del rol de aplicacion)}"
if [ "$APP_DB_USER" = "${PGUSER:-}" ]; then
    echo "migrador: APP_DB_USER no puede ser el mismo usuario que ejecuta las migraciones (${PGUSER})." >&2
    echo "          La aplicacion debe usar un rol distinto, con minimo privilegio." >&2
    exit 1
fi

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
         -v "app_user=${APP_DB_USER}" \
         -v "app_pass=${APP_DB_PASSWORD}" \
         -f "$archivo" \
         -c "INSERT INTO schema_migrations (version, checksum) VALUES ('$version', '$checksum')"
    aplicadas=$((aplicadas + 1))
done

# La contrasena del rol de aplicacion vive en el entorno, no en un script que se aplica una sola vez:
# se sincroniza en cada ejecucion, de modo que rotarla es cambiar APP_DB_PASSWORD y volver a desplegar.
psql_q -v "app_user=$APP_DB_USER" -v "app_pass=$APP_DB_PASSWORD" <<'SQL'
SELECT format('ALTER ROLE %I PASSWORD %L', :'app_user', :'app_pass')
 WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user') \gexec
SQL

echo "migrador: listo ($aplicadas aplicadas, $omitidas ya aplicadas)"
