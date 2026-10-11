# ADR 0002: Sin ORM, con Npgsql y una función almacenada de CRUD

- **Estado:** Aceptada
- **Fecha:** 2026-10-10 (documenta la decisión original del proyecto)
- **Referencias:** especificación del proyecto, [ADR 0008](0008-rol-de-minimo-privilegio.md), `db/001_esquema_usuario.sql`

## Contexto

La especificación pide que el acceso a datos se concentre en la base de datos mediante una **función con un parámetro de operación** (`C`, `R`, `U`, `D`). El dominio es una sola tabla; un ORM aportaría poco y escondería el SQL.

## Decisión

Usar **Npgsql directamente (ADO.NET, sin ORM)** y centralizar el CRUD en `fn_usuario_crud(p_operacion, p_id, p_nombre, p_fecha_nacimiento, p_sexo)`. `UsuarioRepositorio` invoca la función con **parámetros tipados** y no construye SQL con texto del usuario.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Entity Framework Core | Más código y migraciones propias del ORM para una tabla; oculta el SQL y complica el rol de mínimo privilegio |
| Dapper o SQL en línea | Reparte SQL por la aplicación; el rol de la app necesitaría permisos sobre la tabla |
| Un procedimiento por operación | Más objetos que mantener; la especificación pide una sola función con parámetro de operación |

## Consecuencias

- ➕ La aplicación solo necesita `EXECUTE` sobre la función: **no toca la tabla** ([ADR 0008](0008-rol-de-minimo-privilegio.md)).
- ➕ Sin inyección SQL por construcción (parámetros tipados), y las restricciones viven junto a los datos.
- ➖ Parte de las reglas (los `CHECK`) existe también en el código: hay que cambiar ambos lados a la vez.
- ➖ Una función «multiuso» con un parámetro de operación es menos expresiva que operaciones separadas; se aceptó por la especificación. Una operación desconocida lanza `22023`.
- ➖ Probar el repositorio exige una base real: se usa PostgreSQL en contenedor con Testcontainers.
- 🔁 Revisar si el dominio crece a varias tablas con relaciones: ahí un ORM o un micro-ORM empezaría a compensar.
