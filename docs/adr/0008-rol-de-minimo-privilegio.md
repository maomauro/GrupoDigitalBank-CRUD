# ADR 0008: La aplicación se conecta con un rol de mínimo privilegio

- **Estado:** Aceptada
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo H4 (issue [#11](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/11)), `db/002_rol_aplicacion.sql`, [ADR 0002](0002-sin-orm-funcion-almacenada.md)

## Contexto

La aplicación se conectaba con el usuario `POSTGRES_USER`, que la imagen de PostgreSQL crea como **superusuario**. Cualquier fallo de la aplicación, o una inyección que algún día se colara, daría control total sobre la base: leer todo, borrar tablas o crear roles.

## Decisión

Dos roles con responsabilidades separadas:

| Rol | Variable | Qué puede |
|---|---|---|
| `usuarios_admin` | `POSTGRES_USER` | Propietario de la base; ejecuta las migraciones. **La aplicación no lo usa.** |
| `usuarios_app` | `APP_DB_USER` | `LOGIN`, sin superusuario, sin crear bases ni roles, y solo `EXECUTE` sobre `fn_usuario_crud` |

`fn_usuario_crud` es `SECURITY DEFINER` con `search_path = public, pg_temp`, de modo que el rol de la app **no recibe ningún permiso sobre la tabla**. El script `002` retira los permisos de `PUBLIC` sobre la base, el esquema, la tabla y la función. La contraseña llega por el entorno (`APP_DB_PASSWORD`) y el migrador la sincroniza en cada ejecución ([ADR 0007](0007-migraciones-propias-con-checksum.md)).

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Conceder `SELECT/INSERT/UPDATE/DELETE` sobre la tabla al rol de la app | Más superficie: un fallo permitiría leer o borrar la tabla; incompatible con tener toda la lógica en la función |
| Función propiedad de un rol sin superusuario (`NOLOGIN`) | Más estricto, pero obliga a reasignar el dueño de cada objeto nuevo en cada migración; ver riesgo residual |
| Seguir con un solo usuario | Es el problema |

## Consecuencias

- ➕ Verificado con pruebas de integración como el propio rol: no puede `DROP TABLE`, ni leer/insertar/borrar la tabla, ni crear objetos, ni borrar la función, y solo él tiene `EXECUTE`. Una prueba de mutación (dar permisos de más al script) hace fallar 3 de ellas.
- ➕ Rotar la contraseña de la app es cambiar `APP_DB_PASSWORD` y volver a desplegar.
- ➖ **Riesgo residual:** la función pertenece al administrador (superusuario) y corre con sus privilegios. Está acotada porque el SQL es estático y parametrizado y el `search_path` está fijo, pero un dueño sin superusuario sería más estricto.
- ➖ Un volumen existente **no puede convertirse en caliente** (allí el superusuario tenía el mismo nombre que el nuevo rol): se recreó el volumen de desarrollo con respaldo previo verificado. En producción no aplica porque arranca con un volumen nuevo.
- 🔁 Reevaluar el dueño de la función si aparecen más objetos o más aplicaciones que usen la misma base.
