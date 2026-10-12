# ADR 0016: Inicio público y cierre de sesión con Basic Auth

- **Estado:** Aceptada (aprobada el 2026-10-12); complementa al [ADR 0004](0004-autenticacion-minima-basic-auth.md) sin reemplazarlo
- **Fecha:** 2026-10-12
- **Referencias:** [ADR 0004](0004-autenticacion-minima-basic-auth.md), [ADR 0009](0009-cabeceras-http-csp-y-hsts.md); `scripts/probar-acceso.ps1`

## Contexto

La aplicación es la respuesta a una prueba técnica: quien la revisa abre la URL y necesita entender **qué se hizo, con qué herramientas y por qué** antes de pedirle usuario y clave. Con todo detrás del Basic Auth (ADR 0004), lo primero que ve es un cuadro de contraseña.

Además, el menú no tenía «Cerrar sesión». Basic Auth no tiene sesión: el navegador guarda las credenciales y las reenvía hasta que el servidor las rechaza, así que cerrar sesión solo se consigue provocando ese rechazo.

## Decisión

1. **`/` pasa a ser una página de inicio pública** (`InicioController.Index`) que explica la solución: enunciado y cómo se cumple, arquitectura, decisiones, herramientas, calidad, seguridad, producción, proceso y límites. La grilla sigue en `/Usuarios/Consultar`. Es una vista Razor del propio proyecto web (no un archivo estático) para respetar la política de despliegue, que solo admite el compose, el Caddyfile y `db/` (ADR 0013).
2. **Caddy deja fuera del Basic Auth exactamente cuatro rutas**: `/` (exacta), `/css/*`, `/js/*` y `/salir` (exacta), además de `/health` (ADR 0004). Ninguna muestra datos ni consulta el servicio de negocios. Caddy compara la ruta ya limpiada, así que `/css/../Usuarios/Consultar` no se cuela como pública; `scripts/probar-acceso.ps1` prueba nueve variantes de evasión (`..`, `%2e%2e`, `%2f`, barras dobles, barras finales).
3. **`/salir` responde siempre 401** con `WWW-Authenticate: Basic realm="restricted"` (el realm por omisión del `basic_auth` de Caddy) y `Cache-Control: no-store`, con una página que dice «Sesión cerrada». El navegador reintenta con las credenciales guardadas, vuelve a recibir 401, las descarta y muestra el cuadro de usuario y clave (basta con cancelarlo). El menú de todas las páginas trae el enlace «Cerrar sesión».

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Dejar todo detrás del Basic Auth y un README en GitHub | El revisor necesita la explicación en la propia URL, sin depender de acceso al repositorio |
| Página estática servida por Caddy | El paquete de despliegue solo admite compose, Caddyfile y `db/`; ampliarlo debilita el ADR 0013 |
| Inicio de sesión con cookie (ASP.NET Core Identity) para un cierre de sesión real | Mucho más trabajo y superficie para un solo usuario; es el siguiente paso si hay cuentas por persona (ADR 0004) |
| Cerrar sesión con JavaScript que envía credenciales falsas | El comportamiento difiere entre navegadores y exigiría cabeceras de autorización fabricadas desde la página |

## Consecuencias

- ➕ Quien abre la URL ve primero la explicación, con un botón claro hacia la aplicación.
- ➕ La superficie pública es mínima, está documentada y se prueba (67 comprobaciones de acceso, una sección entera de evasiones; las mutaciones del Caddyfile se detectan).
- ➖ **Es una excepción a la regla «todo detrás del Basic Auth salvo `/health`»**: cualquier cambio de esos cuatro patrones debe revisarse y volver a pasar `probar-acceso.ps1`.
- ➖ «Cerrar sesión» es un truco del navegador: puede aparecer un cuadro de usuario y clave que hay que cancelar, y su comportamiento puede variar entre navegadores. Un cierre de sesión real exige cuentas propias.
- ➖ La raíz dejó de ser la grilla: los enlaces de paginación son `/Usuarios/Consultar?pagina=…` y las pruebas se actualizaron.
- 🔁 Revisar con el ADR 0004 si hay más de una persona usuaria.
