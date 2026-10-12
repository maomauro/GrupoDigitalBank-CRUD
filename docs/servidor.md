# Inventario del servidor

Mapa de qué hay en el VPS y dónde vive cada cosa. Verificado contra el servidor el 2026-10-12. No contiene secretos: solo rutas, nombres y estructura. Para operar, ver el [runbook](runbook.md); para las copias, [backups.md](backups.md).

## Máquina

| | |
|---|---|
| Proveedor | Contabo (Cloud VPS 6: 6 vCPU, 12 GB RAM, 200 GB de disco) |
| Sistema | Ubuntu 24.04 LTS |
| Acceso | SSH con llave; un solo usuario (`maomauro`, del grupo `docker`). La IP está en el panel de Contabo y en el DNS de Cloudflare |
| Hora | Europe/Berlin (los crons usan esa hora) |
| Es compartido | Aloja tres proyectos independientes: GDB (este), PortalCV y Colfe |

## Carpetas

```
/srv/sitiosapps/
  gateway/                 repo sitiosapps-gateway (clon de main, solo lectura)
  gdb-crud/                este proyecto: .env, deploy/ y releases/<tag>/ (releases/ACTUAL = versión en marcha)
  Curriculum-Vitae-Web/    PortalCV (repo)
  colfe/                   Colfe (repo)
  _backups/                gdb-crud/  portalcv/  colfe/  premigracion/
/usr/local/sbin/           scripts de root: gdb-crud-{entrada,desplegar,backup,backup-externo,alertas,probar-restauracion}.sh,
                           gdb-crud-politica.py, colfe-entrada.sh, portalcv-entrada.sh
/usr/local/etc/            gdb-crud-age-recipient.txt (llave PÚBLICA de age; la privada no está en el servidor)
/etc/nginx/ssl/            certificados de origen de Cloudflare, uno por subdominio (.pem y .key, la .key en 600 root)
~/.ssh/                    authorized_keys, gateway_deploy (lectura del repo del gateway), known_hosts
~/.config/rclone/          rclone.conf (remoto de copias externas, 600)
~/.config/gdb-crud/        heartbeat-url (dirección secreta del latido, 600) y auth-anterior.env (copia del acceso anterior)
```

## Contenedores y redes

| Proyecto | Contenedores | Redes |
|---|---|---|
| Gateway | `sitiosapps-gateway` (único que publica puertos: 443) | `sitiosapps-edge` |
| GDB | `gdb-crud-proxy-1` (Caddy), `-web-1`, `-negocios-1`, `-db-1` | `gdb-crud_default` (privada) + `sitiosapps-edge` solo el proxy |
| PortalCV | `portalcv-web`, `portalcv-backend-prod`, `portalcv-mariadb-prod` | red privada de PortalCV + `sitiosapps-edge` solo `portalcv-web` |
| Colfe | `colfe-web-1`, `colfe-app-1`, `colfe-db-1` | `colfe_default` (privada) + `sitiosapps-edge` solo `colfe-web` |

Regla: el gateway solo está en `sitiosapps-edge`; nunca en la red privada de un proyecto. Ningún proyecto publica puertos en el host.

## Llaves SSH (`~/.ssh/authorized_keys`)

Una llave **personal** del propietario (acceso de administración, no se restringe) y tres de CI con `restrict,command=` hacia su script de entrada: `gdb-crud-deploy`, `colfe-deploy` y `portalcv-deploy`. Ninguna llave de CI abre una shell. Ver [ADR 0013](adr/0013-politica-de-seguridad-del-despliegue.md) y [ADR 0014](adr/0014-despliegue-detras-del-gateway-de-plataforma.md).

## Tareas programadas (cron del usuario)

| Cuándo (Berlín) | Qué |
|---|---|
| Fin de mes, 03:00 | Copia local de la base de PortalCV |
| Domingos 02:15 | Copia local de la base de Colfe (56 días) |
| Todos los días 02:45 | Copia local de la base de GDB (14 días) |
| Día 1 de cada mes, 04:00 | Copia externa cifrada de GDB |
| Todos los días 05:00 | Alertas del servidor de GDB (latido a Healthchecks.io) |

## Servicios externos

| Servicio | Para qué |
|---|---|
| Cloudflare | DNS, proxy y TLS de `sitiosapps.com`; certificados de origen y Authenticated Origin Pulls (el servidor solo acepta a Cloudflare) |
| GitHub | Código, CI/CD (GitHub Actions) e imágenes (GHCR) |
| Backblaze B2 | Copias externas cifradas (bucket privado) |
| UptimeRobot | Monitor externo de `/health` cada 5 minutos |
| Healthchecks.io | Latido diario del script de alertas |
| SonarCloud | Análisis de calidad en cada PR |

## Pendientes de mantenimiento conocidos

- Reiniciar el servidor para cargar el kernel nuevo (en una ventana: afecta a los tres proyectos).
- Copia externa cifrada para PortalCV y Colfe (hoy solo tienen copia local).
- Política de lista blanca sobre el compose de Colfe y PortalCV, como la de GDB.
