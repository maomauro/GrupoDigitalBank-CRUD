# ADR 0009: Cabeceras de seguridad, CSP estricta y HSTS configurable en el proxy

- **Estado:** Aceptada
- **Fecha:** 2026-10-10
- **Referencias:** hallazgos B4 y H1 (issues [#15](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/15), [#2](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/2)), `Caddyfile`, [ADR 0004](0004-autenticacion-minima-basic-auth.md)

## Contexto

El sitio no enviaba cabeceras de seguridad. Además, el XSS almacenado de H1 se debía a JavaScript en línea en las vistas. Una CSP estricta es una segunda barrera contra XSS, pero **solo es útil si la aplicación no usa nada en línea**.

## Decisión

En el `Caddyfile`, mediante un *snippet* `cabeceras_seguridad` importado en todo el sitio y en `handle_errors`:

- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`: **sin** `unsafe-inline`, `data:` ni comodines.
- `Strict-Transport-Security` con `max-age={$HSTS_MAX_AGE:31536000}`, más `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy` y `Cross-Origin-Resource-Policy: same-origin`, y se elimina `Server`.
- **Las vistas no emiten nada en línea:** la confirmación de Eliminar usa `data-confirmar` y un único `site.js`; y se sustituyó `asp-validation-summary`, que genera `<li style="display:none">`, por el parcial `_ResumenErrores`.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| CSP con `'unsafe-inline'` o *nonces* | `'unsafe-inline'` anula el beneficio; los *nonces* complican el proxy y no hacen falta si no hay nada en línea |
| Cabeceras en la aplicación ASP.NET | Quedarían fuera de los errores 401/502 que genera el proxy, y se duplicaría lo que Caddy ya centraliza |
| HSTS fijo de un año en todos los entornos | Rompe el desarrollo local (ver abajo) |

## Consecuencias

- ➕ Verificado de tres maneras: pruebas de las vistas (nada en línea ni de otro origen), batería de cabeceras por tipo de respuesta (`probar-acceso.ps1`) y un **navegador real** (Chrome y Edge) sin violaciones de CSP, con un control positivo que demuestra que el detector detecta.
- ➖ **Caddy descarta las cabeceras ya fijadas cuando un manejador devuelve un error**, así que el 401 de `basic_auth` salía sin ninguna. Por eso el snippet se importa también en `handle_errors` (cubre además un 502 con `web` caído); `WWW-Authenticate` se conserva. Probado con tres variantes en un Caddy mínimo.
- ➖ **HSTS se aplica al nombre de host sin importar el puerto**: enviarlo desde `https://localhost` forzaría HTTPS en `http://localhost:8080` y `:5080`. Por eso `HSTS_MAX_AGE` vale un año por omisión y `docker-compose.dev.yml` lo fija en `0`. En un primer despliegue se puede bajar (p. ej. 300) antes de subirlo.
- ➖ La CSP obliga a mantener las vistas sin `style=`, `<script>` ni manejadores en línea: está escrito en [`CLAUDE.md`](../../CLAUDE.md) y lo comprueba una prueba.
- 🔁 Si se añade una librería de interfaz o un script de terceros, habrá que ajustar la política con el origen mínimo necesario.
