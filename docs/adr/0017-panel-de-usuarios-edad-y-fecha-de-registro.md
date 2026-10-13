# ADR 0017: Panel de usuarios con sesión, fecha de registro, edad calculada y paginación de 5

- **Estado:** Aceptada (aprobada el 2026-10-13); ajusta la presentación del [ADR 0012](0012-paginacion-en-el-servidor.md) (tamaños de página) y complementa al [ADR 0016](0016-inicio-publico-y-cierre-de-sesion.md) (dónde vive la grilla)
- **Fecha:** 2026-10-13
- **Referencias:** [ADR 0003](0003-contrato-soap-duplicado.md), [ADR 0006](0006-zona-horaria-de-negocio.md), `db/006_fecha_registro.sql`

## Contexto

Con el inicio público (ADR 0016) la raíz dejó de ser la grilla. Al revisar la aplicación se pidieron ajustes de uso: que quien ya inició sesión llegue a la grilla y al resto de la documentación desde el menú, ver cuándo se registró cada usuario y cuántos años tiene, acciones más compactas, un campo de nombre uniforme y una paginación que no estorbe cuando hay pocos usuarios.

## Decisión

1. **Navegación por estado.** Sin sesión el menú trae «Grupo Digital Bank» (la explicación pública, `/`) e «Iniciar sesión». Con sesión: «Grupo Digital Bank» (la misma explicación, protegida, en `/acerca`), «Gestión de usuarios» (`/inicio`, el panel con la grilla y el botón de nuevo usuario) y «Cerrar sesión». `/inicio` y `/acerca` quedan detrás del Basic Auth **por omisión**: no se agregó ninguna ruta pública (siguen siendo las cuatro del ADR 0016). Registrar, modificar y eliminar vuelven al panel.
2. **Fecha de registro.** Columna nueva `usuario.fecha_registro timestamptz NOT NULL DEFAULT now()` (`db/006`, *expand/contract*: la versión anterior de Negocios sigue funcionando). `fn_usuario_crud` la devuelve sin cambios (`SETOF usuario`); `fn_usuario_pagina` cambia su tipo de retorno y se vuelve a crear con los mismos permisos. Los usuarios que ya existían reciben la fecha de la migración, porque su alta real no se conoce.
3. **Edad calculada en Negocios.** `UsuarioDto` gana `FechaRegistro` y `Edad` (campos 4 y 5 del contrato, duplicados a mano en Presentación y comparados por la prueba de contrato). Negocios calcula los **años cumplidos con la fecha de `America/Bogota`** (ADR 0006) y entrega la fecha de registro en esa hora; la base guarda el instante en UTC. Presentación no calcula reglas de tiempo en el servidor.
4. **Edad en vivo en el formulario.** Al escribir la fecha de nacimiento, `site.js` muestra «Edad: N años» (con la fecha de Bogotá) y limita el calendario a `1900-01-01` – hoy. Es solo una ayuda: la validación sigue siendo la de los tres niveles.
5. **Nombre en mayúsculas.** El campo lo convierte mientras se escribe y Presentación lo normaliza al enviar (`ToUpperInvariant`). No es una regla de negocio: Negocios y la base no la exigen.
6. **Paginación de 5 en 5.** Tamaño por omisión 5; opciones 5, 10, 25 y 50 (antes 10, 25 y 50). Los controles solo aparecen cuando hacen falta: con una sola página solo se muestra el total, y cada tamaño mayor se ofrece cuando los usuarios superan al anterior. Negocios y la base no cambian (el tamaño lo pide la web).
7. **Acciones con iconos.** Modificar, Eliminar y Nuevo usuario son iconos SVG en línea, con `title` y `aria-label`, sin `style=` ni recursos externos (la CSP no cambia). El ancho máximo del contenido pasa a 1366 px.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Calcular la edad en Presentación | Tendría que conocer la zona horaria de negocio; la regla de «hoy» vive en un solo lugar (Negocios) |
| Calcular la edad en SQL | Duplicaría la regla de «hoy» y la edad no se guarda: cambia cada día |
| Guardar la edad en la tabla | Se desactualiza; es un dato derivado |
| `fecha_registro` nula para los usuarios antiguos | Obliga a tratar el nulo en todas las capas para un dato de demostración; se prefirió la fecha de la migración, documentada |
| Mayúsculas como `CHECK` en la base | Cambia una regla de datos por una preferencia visual y obliga a migrar los datos existentes |
| Un archivo de iconos o una fuente de iconos | Es un recurso más que servir y cargar; el SVG en línea con clases cumple la CSP sin dependencias |

## Consecuencias

- ➕ La grilla muestra edad y fecha de registro; la edad nunca queda desactualizada.
- ➕ Cambio de esquema compatible hacia atrás; el migrador lo aplica en el despliegue, después de la copia de la base.
- ➖ Las fechas de registro de los usuarios que ya existían son la de la migración, no la de su alta real.
- ➖ El contrato SOAP crece en dos campos que hay que mantener iguales en las dos capas (la prueba de contrato lo vigila).
- ➖ Los nombres ya guardados en minúsculas se conservan así hasta que se modifiquen.
- 🔁 Si se exige que la fecha de nacimiento sea estrictamente anterior a hoy, hay que cambiar a la vez el campo, `UsuarioValidador` y el `CHECK` (un script nuevo).
