# Runbook de GDB: qué hacer cuando algo falla

Guía de operación de `https://gdb-crud.sitiosapps.com`. Está pensada para leerla con prisa: primero **qué alerta sonó**, después **qué mirar** y **qué hacer**. Los comandos se ejecutan en la terminal del VPS (`ssh maomauro@13.140.188.159`) salvo que se indique otra cosa.

## 1. El sistema en una vista

```
Navegador ─► Cloudflare ─► gateway (nginx, único con el 443) ─► proxy (Caddy) ─► web ─► negocios ─► db (PostgreSQL)
```

| Pieza | Contenedor | Qué hace |
|---|---|---|
| gateway | `sitiosapps-gateway` | Termina TLS, exige el certificado de Cloudflare, reparte a los proyectos (repo `sitiosapps-gateway`) |
| proxy | `gdb-crud-proxy-1` | Caddy: usuario y clave (Basic Auth), cabeceras de seguridad |
| web | `gdb-crud-web-1` | La aplicación web (MVC) |
| negocios | `gdb-crud-negocios-1` | Reglas de negocio y acceso a la base (SOAP interno) |
| db | `gdb-crud-db-1` | PostgreSQL |

Todo vive en `/srv/sitiosapps/gdb-crud` (`.env`, y `releases/<tag>/` con el compose de cada versión). `releases/ACTUAL` guarda el tag en marcha.

### Atajo para los comandos de Docker

El compose de GDB usa una carpeta por versión, así que conviene definir esta función una vez por sesión:

```bash
cd /srv/sitiosapps/gdb-crud && V=$(cat releases/ACTUAL) && echo "version en marcha: $V"
gdbc() { IMAGE_TAG=$V docker compose -p gdb-crud --project-directory releases/$V --env-file .env \
  -f releases/$V/docker-compose.yml -f releases/$V/docker-compose.prod.yml "$@"; }
```

Después: `gdbc ps`, `gdbc logs --tail 50 web`, `gdbc restart proxy`.

## 2. Qué alerta sonó

| Alerta | De quién | Qué significa | Ir a |
|---|---|---|---|
| **UptimeRobot: `gdb-crud…/health` DOWN** | Correo de UptimeRobot | Desde fuera, `/health` no responde 200 | [3](#3-el-sitio-no-responde) |
| **Healthchecks: `gdb-crud-servidor` DOWN** con un texto | Correo de Healthchecks | El script de alertas encontró un problema; el texto dice cuál | [4](#4-alerta-del-servidor-healthchecks) |
| **Healthchecks: DOWN sin texto** (o «late») | Correo de Healthchecks | No llegó el latido: el cron no corre, el servidor está apagado o no hay red | [5](#5-no-llega-el-latido) |

## 3. El sitio no responde

1. **Confirmar desde fuera:** `curl -sI https://gdb-crud.sitiosapps.com/health | head -1`. Si da 200, era una falsa alarma o ya se recuperó. Probar también `https://portalcv.sitiosapps.com/health`: si **todos** fallan, el problema es del gateway, de Cloudflare o del servidor, no de GDB.
2. **Estado de los contenedores:** `docker ps --format '{{.Names}}\t{{.Status}}'`. Buscar los que no estén `healthy` o no aparezcan.
3. **Mirar de afuera hacia adentro**, deteniéndose en la primera pieza rota:
   - Gateway: `docker logs --tail 30 sitiosapps-gateway`. Si falta o está reiniciándose: `cd /srv/sitiosapps/gateway && docker compose up -d`.
   - Proxy: `gdbc logs --tail 30 proxy`. Un reinicio suele bastar: `gdbc restart proxy`.
   - Web y negocios: `gdbc logs --tail 50 web` y `gdbc logs --tail 50 negocios`. Reiniciar: `gdbc restart negocios web`.
   - Base de datos: `gdbc logs --tail 50 db`. **No borrar volúmenes ni recrear la base** sin una copia a mano (ver [7](#7-restaurar-una-copia-de-seguridad)).
4. **Cloudflare:** si los contenedores están sanos pero el sitio da 5xx desde internet, revisar https://www.cloudflarestatus.com y que `gdb-crud` siga con el proxy (nube naranja) en el panel DNS.
5. **Comprobar el arreglo:** `/health` da 200 y la página principal pide usuario y clave (`401` sin ellos).

## 4. Alerta del servidor (Healthchecks)

El correo trae el motivo. Cada uno tiene su causa y su arreglo:

| Mensaje | Qué revisar | Qué hacer |
|---|---|---|
| «no hay copias locales» o «la copia local más reciente tiene N h … el backup diario no se está generando» | `tail -20 /srv/sitiosapps/_backups/gdb-crud/backup.log` y `crontab -l \| grep gdb-crud-backup` | Lanzar un backup a mano: `/usr/local/sbin/gdb-crud-backup.sh`. Si falla, el log dice por qué (contenedor `db` caído, disco lleno). Si la línea del cron no está, reinstalarla (ver `docs/backups.md`). |
| «la suma de verificación … no coincide» o «la copia … está dañada» | `ls -l /srv/sitiosapps/_backups/gdb-crud` | Hacer un backup nuevo a mano y probarlo: `/usr/local/sbin/gdb-crud-probar-restauracion.sh`. Investigar el disco si se repite. |
| «el disco está al N %» | `df -h /` y `docker system df` | Ver [6](#6-disco-lleno). |
| «el contenedor … no está sano» | `docker ps -a`, y los logs de ese contenedor | Reiniciarlo con `gdbc restart <servicio>`. Si vuelve a caer, ver sus logs. |
| «la copia externa más reciente tiene N días» o «no se pudo leer la copia externa» | `rclone ls b2backup:sitiosapps-backups-maocifuentes/gdb-crud` y `tail -20 /srv/sitiosapps/_backups/gdb-crud/externo.log` | Lanzar la copia a mano: `RCLONE_REMOTE=b2backup:sitiosapps-backups-maocifuentes/gdb-crud /usr/local/sbin/gdb-crud-backup-externo.sh`. Si `rclone` da error de autenticación, la clave de B2 caducó o se borró: crear otra limitada al bucket y repetir `rclone config create b2backup b2 …` con `read -s`. |
| «la web no responde por internet» | Igual que [3](#3-el-sitio-no-responde) | |

Cuando se arregle, relanzar el script para dejar el latido en verde:

```bash
RCLONE_REMOTE=b2backup:sitiosapps-backups-maocifuentes/gdb-crud URL_SALUD=https://gdb-crud.sitiosapps.com/health /usr/local/sbin/gdb-crud-alertas.sh
```

## 5. No llega el latido

1. ¿Está el servidor encendido? `ssh maomauro@13.140.188.159`. Si no responde, mirar el panel de Contabo.
2. ¿Existe la tarea? `crontab -l | grep gdb-crud-alertas`. Si falta, reinstalarla (ver `docs/backups.md`, «Alertas del servidor»).
3. ¿Corrió? `tail -5 /srv/sitiosapps/_backups/gdb-crud/alertas.log`.
4. ¿Existe la dirección del latido? `ls -l ~/.config/gdb-crud/heartbeat-url` (debe ser `-rw-------`). Si falta, volver a guardarla con `read -s` (ver `docs/backups.md`).
5. Ejecutar el script a mano (comando de arriba): si imprime `latido enviado (ok)`, Healthchecks vuelve a verde.

## 6. Disco lleno

1. `df -h /` y `sudo du -xh /srv /var/lib/docker --max-depth=1 2>/dev/null | sort -h | tail`.
2. Candidatos seguros: imágenes sin usar (`docker image prune -a` pide confirmación; **no** borra las de contenedores en marcha), logs de Docker (ya limitados a 10 MB × 5) y copias antiguas en `_backups` (se rotan solas: 14 días las locales de GDB).
3. **Nunca** `docker volume prune` ni borrar `/var/lib/docker/volumes`: ahí están las bases de datos de todos los proyectos.

## 7. Restaurar una copia de seguridad

**Primero siempre se prueba la copia en una base desechable** (no toca la real):

```bash
/usr/local/sbin/gdb-crud-probar-restauracion.sh                    # la más reciente
/usr/local/sbin/gdb-crud-probar-restauracion.sh /ruta/archivo.sql.gz
```

Debe terminar con `RESTAURACION CORRECTA`.

- **Desde la copia externa (B2):** descargar, descifrar con la llave privada de `age` (que **no** está en el servidor) y probar: pasos en [backups.md](backups.md#cómo-se-restaura-una-copia-externa).
- **Sobre la base real** (solo con el sitio fuera de servicio y una copia fresca de la base actual): el procedimiento exacto está **previsto pero no ensayado en producción**. Antes de necesitarlo, ensayarlo una vez en un entorno desechable con `docker compose` y esta misma copia. En emergencia, avisar y no improvisar: una restauración mal hecha puede borrar datos que sí están.

## 8. Un despliegue falló o hay que volver atrás

- **El despliegue vuelve solo a la versión anterior** si los contenedores no quedan sanos (`Rollback correcto: sigue en marcha …` en el log de GitHub Actions). El sitio queda como estaba.
- **Rollback manual a una versión ya publicada:** GitHub → Actions → **deploy** → *Run workflow* → escribir el tag anterior (por ejemplo `sha-7e37ec67e2ac`; el tag en marcha está en `releases/ACTUAL`). Hace copia previa de la base y recrea los contenedores.
- **Ver qué hubo:** el log del trabajo `entrega / desplegar` en Actions, y `ls -lt /srv/sitiosapps/_backups/gdb-crud` para la copia previa del despliegue.
- **Cuidado con las migraciones:** volver a un código anterior no deshace los cambios de esquema ya aplicados. Los cambios de esquema son aditivos (*expand/contract*) para que la versión anterior siga funcionando.

## 9. Cambiar la clave de acceso a la web

1. En tu PC: `powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1` (genera el hash bcrypt; la clave se teclea en tu terminal).
2. En el VPS, editar `/srv/sitiosapps/gdb-crud/.env`: reemplazar el valor de `AUTH_HASH` (**entre comillas simples**). Nunca la clave en claro.
3. Recrear solo el proxy: `gdbc up -d proxy`. Comprobar que la clave vieja da 401 y la nueva entra.

## 10. Enlaces y dueños

| Qué | Dónde |
|---|---|
| Repo | https://github.com/maomauro/GrupoDigitalBank-CRUD |
| Gateway | https://github.com/maomauro/sitiosapps-gateway |
| Monitor externo | UptimeRobot (monitor `gdb-crud.sitiosapps.com/health`) |
| Latido del servidor | Healthchecks.io (comprobación `gdb-crud-servidor`) |
| Copias externas | Backblaze B2, bucket `sitiosapps-backups-maocifuentes`, carpeta `gdb-crud/` |
| DNS y TLS | Cloudflare (`sitiosapps.com`) |
| Servidor | Contabo (Cloud VPS 6), IP `13.140.188.159` |

Documentos relacionados: [backups.md](backups.md), [arquitectura.md](arquitectura.md), [ADR 0014](adr/0014-despliegue-detras-del-gateway-de-plataforma.md) y [ADR 0015](adr/0015-copia-externa-cifrada-en-b2.md).
