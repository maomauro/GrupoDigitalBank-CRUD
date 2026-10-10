# ADR 0012: Paginación de la grilla en el servidor, con una función nueva

- **Estado:** Aceptada
- **Fecha:** 2026-10-11
- **Referencias:** hallazgo B7 (issue [#18](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/18)), [ADR 0002](0002-sin-orm-funcion-almacenada.md), [ADR 0003](0003-contrato-soap-duplicado.md), [ADR 0008](0008-rol-de-minimo-privilegio.md)

## Contexto

La grilla traía **todos** los usuarios en cada consulta: la base los leía, SOAP los serializaba y la vista dibujaba una fila por cada uno. Con pocos usuarios no importa, pero el costo crece sin límite con los datos, y el diagnóstico lo dejó anotado como baja prioridad (B7). El issue admitía dos salidas: paginar, o documentar que no se hace.

## Decisión

Paginar **en el servidor, de punta a punta**:

1. **Base de datos:** una función nueva, `fn_usuario_pagina(limite, desplazamiento)`, ordenada por `id`, que devuelve en cada fila el total de la tabla. Total y página salen de **una sola sentencia**, es decir, de la misma instantánea. Una página fuera de rango devuelve una fila con `id` nulo que solo trae el total. Valida sus límites por sí sola (SQLSTATE 22023). Es `SECURITY DEFINER` con `search_path` fijo, y solo el rol de aplicación la ejecuta (como `fn_usuario_crud`).
2. **Contrato SOAP:** una operación nueva, `ConsultarPagina(pagina, tamano)`, que devuelve `PaginaUsuarios` (usuarios, total, página y tamaño). `Consultar(id)` no cambia.
3. **Negocios:** valida (página de 1 a 100 000, tamaño de 1 a 100) y **si la página pedida no existe devuelve la última**, no una vacía.
4. **Presentación:** `?pagina=` y `?tamano=` en la URL; solo se aceptan los tamaños 10, 25 y 50 (por omisión 10) y la página se acota a lo que acepta el servicio. Eliminar vuelve a la misma página y tamaño.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| No paginar y documentarlo | Es la salida más barata, pero el costo crece con los datos y una grilla de consulta se espera paginada |
| Paginar solo en la vista (traer todo y partir en memoria) | No ahorra lectura, serialización ni ancho de banda: oculta el problema, no lo resuelve |
| Cambiar `fn_usuario_crud('R')` para aceptar límite y desplazamiento | Rompe la firma y la versión anterior de Negocios durante un despliegue; la regla de *expand/contract* pide agregar, no modificar |
| Dos consultas (página y `count`) | Pueden contradecirse si otro cliente inserta en medio; una sola sentencia lo evita |
| Paginación por cursor (`id > último`) | Mejor para tablas enormes, pero impide saltar a «página N» y complica la vista; a esta escala el desplazamiento basta |
| Mostrar un error ante una página inexistente | Tras eliminar el último usuario de la última página el usuario vería una grilla vacía habiendo usuarios |

## Consecuencias

- ➕ La lectura, la serialización y el HTML son de tamaño constante (≤ 100 filas) sin importar cuántos usuarios haya.
- ➕ Despliegue compatible: la función y la operación son nuevas, así que durante la transición la versión anterior sigue funcionando.
- ➕ El contrato duplicado queda protegido por las pruebas de [ADR 0003](0003-contrato-soap-duplicado.md): la operación y el DTO nuevos se comparan por reflexión y se ejercen con el cliente real.
- ➖ El desplazamiento (`OFFSET`) recorre las filas que salta: el costo de páginas muy lejanas crece con su número. Con el tope de 100 000 páginas y un índice por `id` es aceptable a esta escala; si la tabla creciera a millones de filas habría que pasar a paginación por cursor.
- ➖ Una fila que se inserta o elimina mientras se navega puede aparecer dos veces u omitirse entre páginas (propio del desplazamiento).
- ➖ Editar y registrar vuelven a la primera página, no a la de origen; solo Eliminar conserva la página.
- 🔁 Revisar si la tabla supera cientos de miles de filas, o si se añade búsqueda o filtros (que cambiarían la forma de paginar).
