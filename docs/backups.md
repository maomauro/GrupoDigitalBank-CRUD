# Copias de seguridad y restauración

Este documento describe qué copias existen, dónde viven y cómo se restauran. El código del proyecto no se respalda aquí: está en GitHub y las imágenes en GHCR.

## Qué existe

| Copia | Qué contiene | Cuándo | Dónde | Cuántas |
|---|---|---|---|---|
| **Local diaria** (`deploy/backup.sh`) | `pg_dump` comprimido (`usuarios_AAAA-MM-DD_HHMMSS.sql.gz`) con su `.sha256` | Todos los días 02:45 (hora del servidor, Berlín) y antes de cada despliegue | `/srv/sitiosapps/_backups/gdb-crud` | 14 días |
| **Externa mensual** (`deploy/backup-externo.sh`) | La copia local más reciente y el `.env` de GDB, **cifrados con age** | Día 1 de cada mes, 04:00 | Bucket privado de Backblaze B2, carpeta `gdb-crud/` | Las 2 últimas de cada tipo |

La copia externa existe porque, si el VPS se pierde, las copias locales se pierden con él. Los certificados de origen no se respaldan: se vuelven a emitir desde Cloudflare.

## Cifrado

Se usa [age](https://github.com/FiloSottile/age) con un par de llaves:

- La llave **pública** está en el servidor (`/usr/local/etc/gdb-crud-age-recipient.txt`) y solo sirve para cifrar.
- La llave **privada** la guarda el responsable, **fuera del servidor** (gestor de contraseñas y una copia aparte). Sin ella las copias externas no se pueden descifrar. Nunca se versiona ni se copia al VPS.

## Cómo se restaura una copia externa

1. Descargar la copia que se necesite (en un equipo con `rclone` configurado hacia el bucket, o desde la consola de B2):

   ```bash
   rclone ls b2backup:NOMBRE-DEL-BUCKET/gdb-crud
   rclone copy b2backup:NOMBRE-DEL-BUCKET/gdb-crud/usuarios_AAAA-MM-DD_HHMMSS.sql.gz.age .
   ```

2. Descifrar con la llave privada (pide el archivo de la llave, no una contraseña):

   ```bash
   age -d -i clave-backups-sitiosapps.txt -o usuarios.sql.gz usuarios_AAAA-MM-DD_HHMMSS.sql.gz.age
   ```

3. Comprobar que se puede restaurar, sobre una base desechable que no toca la real:

   ```bash
   /usr/local/sbin/gdb-crud-probar-restauracion.sh usuarios.sql.gz
   ```

   Debe terminar con `RESTAURACION CORRECTA`.

4. Para recuperar un servidor nuevo, cargar el volcado en la base real con `psql` (rol administrador) **después** de levantar el stack vacío y antes de abrirlo al público. El `.env` descifrado (`env_*.age`) se coloca en `/srv/sitiosapps/gdb-crud/.env` con permiso `600`.

## Verificación periódica

- El primer día hábil de cada mes: comprobar que `externo.log` (`/srv/sitiosapps/_backups/gdb-crud/externo.log`) terminó sin errores y que el bucket tiene las dos copias más recientes.
- Una vez cada trimestre: restaurar de verdad una copia externa descargada, con los pasos de arriba. **Una copia que nunca se ha restaurado no cuenta.**

## Instalación en el servidor (una vez)

```bash
sudo apt-get install -y rclone age
echo 'age1...(llave pública)' | sudo tee /usr/local/etc/gdb-crud-age-recipient.txt
rclone config create b2backup b2 account=ID_DE_LA_CLAVE key=CLAVE   # mejor con read -s, ver abajo
( crontab -l; echo '0 4 1 * * RCLONE_REMOTE=b2backup:NOMBRE-DEL-BUCKET/gdb-crud /usr/local/sbin/gdb-crud-backup-externo.sh >> /srv/sitiosapps/_backups/gdb-crud/externo.log 2>&1' ) | crontab -
```

La clave de aplicación de B2 se limita a **un solo bucket** y se escribe con `read -s` (sin dejarla en el historial). El archivo `~/.config/rclone/rclone.conf` debe tener permiso `600`. Para rotar la clave: crear otra en B2, repetir `rclone config create` y borrar la anterior.

La prueba automática del script es `scripts/probar-backup-externo.sh` (en Windows, `.ps1`).
