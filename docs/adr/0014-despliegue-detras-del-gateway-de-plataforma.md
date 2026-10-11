# ADR 0014: Despliegue detrás del gateway de plataforma

- **Estado:** Aceptada (aprobada el 2026-10-11)
- **Fecha:** 2026-10-11
- **Referencias:** reemplaza al [ADR 0011](0011-despliegue-detras-del-nginx-compartido.md); [ADR 0013](0013-politica-de-seguridad-del-despliegue.md); hallazgo H-001 y ADR 0001 del portafolio (gateway de plataforma); repositorio `sitiosapps-gateway`

## Contexto

El ADR 0011 hizo entrar a este proyecto por `portalcv-nginx-prod`, el nginx que PortalCV declaraba en su compose. Eso acoplaba a todos los proyectos del VPS a un despliegue ajeno: un error en el compose de PortalCV o un `--force-recreate` de su frontend dejaba sin servicio a los demás.

El 2026-10-11 el nginx de borde pasó a un **gateway de plataforma** independiente (`/srv/sitiosapps/gateway`, repositorio `sitiosapps-gateway`):

- Es el único que publica el 443; escucha por nombre (un vhost por proyecto) y rechaza el handshake de cualquier nombre desconocido.
- Solo acepta a Cloudflare: exige el certificado de cliente de Authenticated Origin Pulls (mTLS), y toma la IP real de `CF-Connecting-IP` únicamente desde los rangos de Cloudflare.
- Los proyectos de entrada se unen a la red `sitiosapps-edge` (172.29.0.0/24) con un alias; `proxy_pass` siempre con variable y `resolver`, de modo que un destino ausente solo afecta a su sitio.

## Decisión

El `proxy` de este proyecto (Caddy) se une a la red compartida **`sitiosapps-edge`** con el alias `gdb-crud-web`, en lugar de `curriculum-vitae-web_portalcv-net-prod`. El vhost de producción vive en `sitiosapps-gateway/nginx/conf.d/gdb-crud.conf` y añade un límite de tasa (5 peticiones por segundo por IP, ráfaga de 30) que protege el Basic Auth sin depender de Cloudflare.

Todo lo demás del ADR 0011 se mantiene: Caddy como proxy interno solo HTTP con `trusted_proxies`, ningún puerto publicado, despliegue por SSH con llave restringida y política de seguridad (ADR 0013). `deploy/nginx/gdb-crud.conf` queda como réplica de desarrollo y de prueba (servicio `borde` y `probar-despliegue.sh`), no como fuente de producción.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Seguir detrás de `portalcv-nginx-prod` | Mantiene el acoplamiento que motivó el gateway |
| Que el `proxy` publique su propio puerto | Docker salta ufw al publicar; el origen quedaría alcanzable sin Cloudflare |
| Mantener el vhost en este repositorio | Los vhosts son parte de la seguridad de todos los proyectos; se revisan en un solo lugar (`verificar-config.sh` lo impone) |

## Consecuencias

- ➕ Un despliegue de PortalCV o de Colfe ya no puede tumbar este sitio, ni al revés.
- ➕ El origen solo acepta a Cloudflare; el límite de tasa por IP real funciona en el origen.
- ➖ El despliegue depende de que exista la red `sitiosapps-edge` (la crea el gateway): hay que levantar el gateway antes del primer despliegue.
- ➖ El vhost y el proyecto viven en repositorios distintos; un cambio de puerto o de alias exige tocar ambos.
- 🔁 Revisar si la política (`deploy/politica.py`) debe admitir más de una red cuando aparezcan proyectos que compartan servicios.
