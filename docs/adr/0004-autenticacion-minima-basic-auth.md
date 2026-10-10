# ADR 0004: Autenticación mínima con Basic Auth en el proxy

- **Estado:** Aceptada (provisional: se revisa si hay más de una persona usuaria o hace falta auditoría)
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo H2 (issue [#12](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/12)), [ADR 0009](0009-cabeceras-http-csp-y-hsts.md)

## Contexto

La especificación no pedía autenticación, pero el sistema se publica en internet y **cualquiera con la URL podía crear, modificar o borrar usuarios** (hallazgo de severidad alta). Hacía falta cerrar el acceso anónimo con el menor costo posible, sin construir un sistema de cuentas.

## Decisión

Proteger todo el sitio con **Basic Auth en Caddy**: un usuario y una clave compartidos, con la clave guardada solo como hash bcrypt (`AUTH_HASH`) en el `.env`. `/health` queda abierto (coincidencia exacta) para monitores y la prueba de humo del despliegue. El proxy elimina el encabezado `Authorization` antes de reenviar a la aplicación. Si falta o es inválido el hash, el proxy **no arranca**: nunca queda expuesto sin protección.

## Alternativas consideradas

| Alternativa | Por qué no (por ahora) |
|---|---|
| ASP.NET Core Identity (login propio) | Cuentas, recuperación de clave, sesiones y roles: mucho más trabajo y superficie de ataque para una persona usuaria |
| Proveedor externo (OIDC: Google, Microsoft) | Dependencia externa y configuración de aplicación registrada; desproporcionado a esta escala |
| Restringir por IP | Frágil si la IP cambia; no protege frente a quien comparta la red |
| Autenticación en la aplicación .NET | Duplica en código lo que el proxy ya resuelve con un bloque de configuración |

## Consecuencias

- ➕ Cierra el hueco **sin cambiar código de la aplicación**; probado en un navegador real y con una batería de comprobaciones de acceso (`scripts/probar-acceso.ps1`).
- ➖ **Un solo usuario compartido:** no distingue personas, no hay «cerrar sesión» (el navegador recuerda la clave) ni auditoría por usuario.
- ➖ **Caddy no bloquea tras intentos fallidos.** El bcrypt de coste 14 hace que cada intento tarde ≈0,9 s, pero no hay bloqueo de IP. Mitigar con una clave larga (≥ 20 caracteres) y, en el VPS, `fail2ban` sobre el log de Caddy o un límite de tasa.
- ➖ La clave debe generarse con `scripts/generar-hash-auth.ps1`: pasarla por la tubería de PowerShell 5.1 antepone un BOM y un CRLF y produce un hash de otra cadena (descubierto durante la implementación).
- 🔁 Revisar cuando haya más de una persona usuaria, requisitos de auditoría o cuando se añadan funciones que dependan de quién actúa.
