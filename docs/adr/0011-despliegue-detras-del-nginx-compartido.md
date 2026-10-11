# ADR 0011: Despliegue detrás del nginx compartido del VPS

- **Estado:** Reemplazada por [0014](0014-despliegue-detras-del-gateway-de-plataforma.md) (aprobada el 2026-10-10; implementada en la fase 7)
- **Fecha:** 2026-10-10
- **Referencias:** issues [#21](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/21) y [#22](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/22), [plan de trabajo](../plan-de-trabajo.md) fases 6 a 8, [ADR 0004](0004-autenticacion-minima-basic-auth.md), [ADR 0009](0009-cabeceras-http-csp-y-hsts.md)

## Contexto

El plan original suponía un servidor propio, con Caddy publicando 80 y 443 y emitiendo certificados con Let's Encrypt. La inspección del VPS real (2026-10-10) mostró otra cosa:

- Es un servidor **compartido** que ya aloja PortalCV y Colfe.
- `portalcv-nginx-prod` es el **único** contenedor que publica 80 y 443. Los demás se alcanzan por una red Docker compartida (`curriculum-vitae-web_portalcv-net-prod`), cada uno con un alias.
- Cloudflare está delante de todo `sitiosapps.com`; los certificados son de Origen de Cloudflare, instalados a mano en `/etc/nginx/ssl/`.
- El convenio del servidor es `<proyecto>.sitiosapps.com`, carpetas en `/srv/sitiosapps/` y un vhost por proyecto (`docker/<proyecto>.conf`) con `resolver` + `set $upstream`, porque un `proxy_pass` fijo a un contenedor ausente impide arrancar el nginx y tumba a todos.

Un Caddy que publicara 80 y 443 no podría arrancar: los puertos están ocupados.

## Decisión

Entrar por el nginx compartido y **conservar Caddy como proxy interno**:

1. El `proxy` no publica puertos. Se une a la red compartida con el alias `gdb-crud-web`; `db`, `negocios` y `web` se quedan en la red interna del proyecto.
2. Caddy escucha solo HTTP (`http://{$DOMINIO}`) y confía en los proxies privados (`trusted_proxies`) para conservar el esquema y la IP reenviados. Mantiene el Basic Auth, las cabeceras, la CSP y el HSTS tal como están probados.
3. TLS lo terminan Cloudflare y el nginx compartido, con un certificado de origen para `gdb-crud.sitiosapps.com`. El vhost se añade en el repositorio de PortalCV siguiendo el patrón del servidor.
4. El compose base no declara `ports`; los de `proxy` pasan al compose de desarrollo, y CI comprueba que la configuración de producción no publica nada.
5. Despliegue por SSH con una llave **restringida** (`restrict,command=`) a un script de entrada que solo acepta `desplegar <tag>`.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Caddy publicando 80/443 | Los puertos los ocupa el nginx compartido; habría que moverlo o detener los otros proyectos |
| Quitar Caddy y mover Basic Auth, cabeceras y CSP al nginx | Se pierde lo ya probado (`probar-acceso.ps1`, `probar-navegador.ps1`) y se mezclan reglas de un proyecto en el nginx de otros |
| Caddy en otro puerto y Cloudflare apuntando a él | Habría que abrir un puerto adicional al exterior y configurarlo en Cloudflare, sin ventaja frente a entrar por el nginx que ya está |
| Un VPS aparte para este proyecto | Costo adicional sin necesidad a esta escala |

## Consecuencias

- ➕ Cero puertos nuevos expuestos; el proyecto convive con los otros sin tocar su infraestructura más allá de un vhost.
- ➕ Se conserva el diseño de seguridad (ADR 0004 y 0009) y su batería de pruebas.
- ➖ **Dos proxies en cadena:** hay que verificar que el esquema (`X-Forwarded-Proto`) y la IP del cliente lleguen intactos a la aplicación; es una hipótesis, no un hecho comprobado, hasta probarlo con un nginx delante.
- ➖ Añadir el vhost exige un PR en otro repositorio y **recrear** `portalcv-nginx-prod` (unos segundos sin servicio para PortalCV y Colfe), una sola vez y en ventana.
- ➖ La mitigación de fuerza bruta del ADR 0004 cambia: Caddy solo ve la IP de nginx, así que `fail2ban` sobre su log no sirve; se usa una regla de límite de tasa en Cloudflare.
- ➖ El HSTS lo emite solo Caddy; el vhost no lo repite.
- 🔁 Revisar si PortalCV deja el nginx compartido o si el proyecto necesita más de un usuario (ADR 0004).
