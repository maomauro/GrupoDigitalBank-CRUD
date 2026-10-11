# ADR 0013: Política de seguridad para el paquete de despliegue

- **Estado:** Propuesta (se aprueba al fusionar el PR de la fase 7)
- **Fecha:** 2026-10-11
- **Referencias:** [ADR 0011](0011-despliegue-detras-del-nginx-compartido.md), [ADR 0008](0008-rol-de-minimo-privilegio.md), issue [#22](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/22), plan de trabajo (fase 7)

## Contexto

El ADR 0011 decidió desplegar por SSH con una llave que **solo puede ejecutar un script de entrada** (`restrict,command=`). Al construirlo apareció el hueco: el paquete que entrega GitHub Actions incluye los archivos de Compose, y en un servidor donde el usuario pertenece al grupo `docker` y que comparte el daemon con PortalCV y Colfe, **un compose arbitrario equivale a root**: un contenedor privilegiado, un montaje de `/` o de `/var/run/docker.sock`, o unirse a la red de otro proyecto basta. Restringir la llave a un comando sirve de poco si ese comando ejecuta lo que el paquete diga.

Quien puede entregar un paquete es quien controla la cuenta de GitHub, el *environment* `production` o la rama `main`. Ese es el límite de confianza que no se puede eliminar: lo que sí se puede es **reducir lo que un paquete malicioso puede hacer**.

## Decisión

1. **Los scripts que ejecuta la llave los instala root, fuera de la carpeta del proyecto** (`/usr/local/sbin/gdb-crud-*`, `deploy/instalar.sh`). Un paquete no puede reemplazarlos, ni siquiera el de la propia llave; cambiarlos exige un PR revisado y una copia manual.
2. **La entrada solo entiende `desplegar <tag>`** (tag validado con una expresión regular) y revisa el paquete **antes de extraerlo**: solo archivos regulares y carpetas, con una lista cerrada de nombres (`docker-compose.yml`, `docker-compose.prod.yml`, `Caddyfile`, `db/*`). Enlaces, rutas con `..` o absolutas y archivos extra se rechazan.
3. **Una política de lista blanca valida el compose ya resuelto** (`docker compose config --format json`) antes de levantar nada (`deploy/politica.py`): solo los 5 servicios esperados; imágenes propias del registro y repositorio esperados u oficiales de `postgres` y `caddy`; ningún `ports`, `privileged`, `network_mode`, `pid`, `cap_add`, `devices`, `build`…; montajes solo de volúmenes con nombre del proyecto o de carpetas dentro de la entrega, nunca `/` ni el socket de Docker; redes solo la interna y, únicamente para el `proxy`, la compartida con el alias exacto `gdb-crud-web`.
4. **Sin credenciales persistentes en el servidor.** Para descargar las imágenes de GHCR se usa el `GITHUB_TOKEN` del propio flujo, enviado por la entrada estándar (no por la línea de comandos) y usado en una carpeta de configuración de Docker temporal que se borra al terminar. No hay un token personal de larga vida que pueda filtrarse del servidor.
5. **El nginx compartido no se toca en cada despliegue**: resuelve el alias en cada petición (ADR 0011).

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Solo la llave restringida (ADR 0011 a secas) | El paquete ejecutaría cualquier compose: equivale a darle root a quien entregue el paquete |
| Dejar el compose en el servidor y que el despliegue solo cambie el tag | Cierra el hueco por completo, pero cada cambio de infraestructura exigiría una copia manual y se pierde la reproducibilidad por commit; la política consigue casi lo mismo con cambios revisados en el repositorio |
| Lista negra de opciones peligrosas | Siempre falta una; una lista blanca falla cerrada ante lo que no conoce |
| Un PAT de lectura de paquetes en el servidor | Credencial de larga vida que vive junto a otros proyectos; el `GITHUB_TOKEN` por ejecución no deja nada |
| Un usuario de despliegue aparte sin acceso a Docker | No es posible: desplegar requiere Docker, y pertenecer al grupo `docker` ya es equivalente a root |

## Consecuencias

- ➕ Una llave filtrada **no abre una shell** ni puede ejecutar nada distinto de `desplegar <tag>`; y aun con la llave, un compose o paquete hostil se rechaza antes de ejecutarse. Un atacante quedaría limitado a desplegar versiones válidas de este proyecto.
- ➕ La política y la entrada tienen pruebas de punta a punta con 12 composes y 6 paquetes maliciosos (`scripts/probar-despliegue.sh`, en el CI) y se verificó con mutaciones que la prueba falla si se debilitan.
- ➖ **Riesgo residual que no se elimina:** quien controle el repositorio o el *environment* puede publicar una **imagen** maliciosa con la etiqueta de una versión: correría confinada por el compose (sin privilegios, sin puertos, sin montajes del host, solo con la red interna y el alias) pero con acceso a la base de este proyecto. Lo mitigan el 2FA de la cuenta de GitHub y las alertas de Dependabot, no este mecanismo.
- ➖ La política es **estricta a propósito**: cambiar un servicio, un volumen o la imagen base (por ejemplo una actualización mayor de `postgres`) puede exigir editar `deploy/politica.py` y reinstalarlo en el servidor. Es el costo de fallar cerrado.
- ➖ Los cuatro scripts instalados pueden quedar desactualizados respecto al repositorio; `instalar.sh` imprime sus sumas SHA-256 para compararlas.
- ➖ Depende de `python3` en el servidor (viene en Ubuntu Server) y del formato JSON de `docker compose config`.
- 🔁 Revisar si se añade otro proyecto al mismo mecanismo, si cambia el modelo de acceso a Docker del servidor (por ejemplo Docker rootless) o si aparece la protección de ramas y de *environments* (ADR 0005), que reduciría el riesgo residual.
