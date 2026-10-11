# ADR 0007: Migraciones propias con checksum y un servicio de un solo uso

- **Estado:** Aceptada
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo H5 (issue [#10](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/10)), `db/migrate.sh`, `scripts/probar-migrador.ps1`

## Contexto

El esquema se aplicaba con `docker-entrypoint-initdb.d`, que **solo corre al crear el volumen**: cualquier cambio posterior obligaba a `docker compose down -v`, que borra los datos. Hacía falta un mecanismo para evolucionar el esquema sin perderlos, tanto en desarrollo como en producción.

## Decisión

Un script POSIX, `db/migrate.sh`, ejecutado por un servicio `migrador` de Compose (imagen `postgres:17-alpine`) que termina al acabar:

- Aplica en orden los `db/NNN_*.sql` que falten, **cada uno en una sola transacción junto con su registro** en `schema_migrations(version, checksum, aplicada_en)`: si falla, no queda nada a medias.
- Guarda el **checksum sha256** y **aborta si un script ya aplicado cambió**: los aplicados no se editan; todo cambio es un script nuevo.
- `negocios` depende de `migrador: service_completed_successfully`: una migración rota **impide** que arranque la aplicación.
- Exige `APP_DB_USER` y `APP_DB_PASSWORD` y **sincroniza la contraseña del rol de la app en cada ejecución**, para que rotarla sea cambiar el entorno y redesplegar ([ADR 0008](0008-rol-de-minimo-privilegio.md)).

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| `docker-entrypoint-initdb.d` | Solo en la creación del volumen: es el problema que se quiere resolver |
| DbUp, Flyway o migraciones de EF Core | Funcionan, pero añaden una dependencia o un runtime (Java, .NET) y el proyecto no tiene ORM ([ADR 0002](0002-sin-orm-funcion-almacenada.md)); el migrador cabe en ~80 líneas de `sh` |
| Aplicar los scripts a mano | Propenso a error y no reproducible |

## Consecuencias

- ➕ Sin dependencias nuevas; el esquema evoluciona sin recrear volúmenes; el checksum detecta ediciones accidentales; probado en 8 escenarios (`probar-migrador.ps1`: vacía, repetida, nuevo, con error, modificado, base existente, rol y validación de entrada).
- ➖ **No hay migraciones «hacia abajo»**: se corrige con un script nuevo y los cambios deben ser *expand/contract* (compatibles hacia atrás durante un despliegue).
- ➖ No hay bloqueo (advisory lock) entre migradores concurrentes; se asume un solo migrador por despliegue.
- ➖ Los scripts deben tener finales de línea LF (`.gitattributes`) y, cuando sea razonable, ser idempotentes (lo que permitió adoptar el migrador sobre una base existente sin recrearla).
- ➖ Los números se asignan por orden de fusión: si dos ramas crean el mismo, la segunda renumera (ocurrió con B6 y #41).
- 🔁 Reconsiderar una herramienta estándar si el esquema crece a decenas de scripts o si participan varias personas escribiendo migraciones a la vez.
