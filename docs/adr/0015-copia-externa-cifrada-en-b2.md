# ADR 0015: Copia externa cifrada con age en Backblaze B2

- **Estado:** Aceptada (aprobada el 2026-10-11)
- **Fecha:** 2026-10-11
- **Referencias:** [docs/backups.md](../backups.md), [ADR 0011](0011-despliegue-detras-del-nginx-compartido.md), [ADR 0013](0013-politica-de-seguridad-del-despliegue.md), plan de trabajo fase 9

## Contexto

Las copias locales (`deploy/backup.sh`) viven en el mismo VPS que la base: si el servidor se pierde, se pierden juntas. Hace falta una copia fuera del servidor, con pocos históricos, que no exponga datos de personas ni secretos.

Se intentó primero Google Drive con `rclone`. La credencial compartida de `rclone` para Drive se retira durante 2026, y una credencial propia exige publicar la app OAuth, lo cual requiere una página de privacidad, una página principal y dominios autorizados; sin publicar, el token caduca a los 7 días y el trabajo mensual se rompería en silencio.

## Decisión

Copia **mensual** (día 1, 04:00 hora del servidor) a un bucket **privado de Backblaze B2**, con una clave de aplicación **limitada a ese bucket**. Antes de salir del servidor, la copia de la base y el `.env` se **cifran con age** usando una llave pública; la llave privada vive fuera del VPS. Se conservan las **2 últimas** de cada tipo, y la subida se verifica por hash antes de borrar nada. El script (`deploy/backup-externo.sh`) lo instala root, como los demás (ADR 0013).

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Google Drive con `rclone` | Credencial compartida en retirada; la propia exige verificación y páginas públicas; el token de una app sin publicar caduca a los 7 días |
| Cuenta de servicio de Google | No tiene cuota de almacenamiento en un Drive personal |
| Subir en claro y confiar en el cifrado del proveedor | Los datos de personas y los secretos quedarían legibles para el proveedor y para quien robe la clave de la API |
| Solo copias locales | No sobreviven a la pérdida del servidor |

## Consecuencias

- ➕ Una copia fuera del servidor, ilegible sin la llave privada, y una clave de B2 que solo toca un bucket.
- ➕ El token de B2 no caduca; no hay pantalla de consentimiento que mantener.
- ➖ **Si se pierde la llave privada, las copias externas son irrecuperables.** Debe estar en un gestor de contraseñas y en una copia aparte.
- ➖ Frecuencia mensual: ante un desastre se pueden perder hasta 30 días de datos; las copias locales diarias cubren el resto mientras el VPS exista.
- 🔁 Revisar el límite de 10 GB gratuitos si se incorporan las bases de PortalCV y Colfe, y la política de 2 históricos si se necesita más retención.
