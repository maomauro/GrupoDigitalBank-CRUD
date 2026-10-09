# Plan de trabajo: de local a producción en VPS

Base: [diagnostico-2026-10-09.md](diagnostico/diagnostico-2026-10-09.md). Cada hallazgo (H1…H8 alta/media, B1…B8 baja) queda asignado a una fase.

## Supuestos (cambiar si no aplican)

| Tema | Decisión por defecto |
|---|---|
| Equipo local | Windows 11, PowerShell |
| VPS | Ubuntu 24.04 LTS, acceso root inicial por SSH, IP pública fija |
| Dominio | Subdominio propio (ej. `usuarios.midominio.com`) con registro A hacia el VPS |
| Remoto Git | GitHub, repo privado, rama `main` protegida |
| Registro de imágenes | GitHub Container Registry (`ghcr.io`) |
| Despliegue | GitHub Actions → SSH al VPS → `docker compose pull && up -d` |
| Migraciones | Scripts SQL numerados + contenedor one-shot con `psql` (sin dependencias nuevas) |
| Autenticación mínima | Basic Auth en Caddy (fase 3); login propio queda como mejora futura |

## Resumen de fases

| # | Fase | Resultado | Esfuerzo |
|---|---|---|---|
| 0 | Herramientas locales | Máquina lista para compilar, probar y usar Docker | 1–2 h |
| 1 | Repositorio local | Git inicializado, convenciones y commit base | 1 h |
| 2 | Ejecutar en local | App funcionando con Docker y sin Docker | 1–2 h |
| 3 | Remediación del diagnóstico | Hallazgos corregidos con pruebas | 2–3 días |
| 4 | Repositorio remoto | GitHub con protecciones, secretos y Dependabot | 1 h |
| 5 | CI | Build + tests + escaneo en cada PR | 3–4 h |
| 6 | Preparar el VPS | Servidor endurecido con Docker, DNS y firewall | 2–3 h |
| 7 | CD | Imágenes en GHCR y despliegue automático con rollback | 4–6 h |
| 8 | Primera publicación | Sistema en `https://<dominio>` | 1–2 h |
| 9 | Operación | Backups, monitoreo, runbooks | 3–4 h |
| 10 | Cierre | Documentación y checklist de aceptación | 2 h |

Orden obligatorio: 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10. Las fases 6 (VPS) y 4 (GitHub) pueden hacerse en paralelo con la 3.

## Prioridades

- **P0, bloqueante:** sin esto no se puede avanzar ni publicar de forma segura.
- **P1, antes de producción:** debe estar listo antes del primer despliegue.
- **P2, primeras semanas en producción:** conviene cerrar pronto, pero no impide publicar.
- **P3, backlog:** mejora diferible.

| Prio | Ítem | Fase |
|---|---|---|
| P0 | Instalar y verificar herramientas | 0 |
| P0 | `git init`, `.gitignore`, `CLAUDE.md`, guía de Git, commit base | 1 |
| P0 | Primer build y ejecución local (el proyecto nunca se ha compilado) | 2 |
| P0 | H1 XSS en Eliminar | 3.1 |
| P0 | Repo remoto, protección de `main`, secretos | 4 |
| P1 | H2 autenticación (Basic Auth) | 3.2 |
| P1 | H4 rol de BD de mínimo privilegio | 3.4 |
| P1 | H5 migraciones | 3.5 |
| P1 | H6 claves de Data Protection | 3.6 |
| P1 | H7 pruebas mínimas (validador, servicio, repositorio, regresión H1) | 3.7 |
| P1 | B3 healthchecks, B4 cabeceras de seguridad, B5 sin contraseña en `appsettings` | 3.8 |
| P1 | CI (fase 5) | 5 |
| P1 | VPS endurecido, DNS, Docker | 6 |
| P1 | CD con smoke test y rollback | 7 |
| P1 | Backup diario y restauración probada | 9.1 |
| P1 | Primera publicación | 8 |
| P2 | Monitoreo externo, runbook, rotación de secretos | 9 |
| P2 | H8 arquitectura y ADRs, B1 versiones fijas, B6 zona horaria, B8 WSDL solo en desarrollo | 1 y 3.8 |
| P2 | Cobertura ≥ 80 %, Trivy, CodeQL, hadolint | 5 |
| P3 | B7 paginación, B2 prueba de contrato SOAP, login propio (Identity/OIDC), métricas DORA | 3.8 y 10 |

Camino crítico (el mínimo para publicar sin riesgo): fases 0 → 1 → 2 → H1 → 4 → H2/H4/H5/H6 → pruebas mínimas → CI → 6 → 7 → 8 → backup.

## Documentos de soporte del proyecto

| Documento | Para qué | Cuándo se crea |
|---|---|---|
| [CLAUDE.md](../CLAUDE.md) | Contexto del proyecto para Claude Code: arquitectura, comandos, convenciones, qué nunca hacer | Ya creado; se actualiza al cerrar cada fase |
| [ciclo-de-vida-desarrollo.md](ciclo-de-vida-desarrollo.md) | Ciclo local → GitHub, brechas del ambiente, definiciones de listo/terminado y decisiones abiertas | Ya creado; **primer foco de trabajo** (fases 0–5) |
| [git-guia-tecnica.md](git-guia-tecnica.md) | Uso de Git, ramas locales y remotas, flujo, protecciones y recetas | Ya creado; consultar desde la fase 1 |
| [diagnostico-2026-10-09.md](diagnostico/diagnostico-2026-10-09.md) | Hallazgos de partida | Existente |
| `arquitectura.md`, `adr/` | Decisiones y diagramas C4 | Fase 3.8 |
| `runbook.md` | Operación y recuperación | Fase 9.4 |

---

## Fase 0. Herramientas locales

### 0.0 Primero verificar, después instalar

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1
```

[scripts/verificar-herramientas.ps1](../scripts/verificar-herramientas.ps1) no instala ni modifica nada: marca `OK`, `FALTA` (obligatoria, con el comando `winget` exacto), `OPCIONAL` o `AVISO`, y comprueba que Docker esté en ejecución, que haya un SDK 10, la identidad de Git, la sesión de `gh`, las llaves SSH y que los puertos 80/443 estén libres. Termina con código 1 si falta algo obligatorio. **Instala solo lo que marque `FALTA`**; la sección 0.1 es una referencia.

¿Todo corre en Docker? En producción y para probar el stack completo, sí. El SDK de .NET no es para ejecutar la app sino para el ciclo de desarrollo: `dotnet build`, `dotnet test`, `dotnet format`, `user-secrets` y depuración en VS Code. `psql` y la instalación local de PostgreSQL son opcionales porque se puede entrar con `docker compose exec db psql`. Las herramientas del VPS (Docker, `ufw`, etc.) se instalan en el servidor en la fase 6, no en tu equipo.

### 0.1 Instalación (PowerShell como administrador)

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.10 -e
winget install --id Docker.DockerDesktop -e        # requiere WSL2; reiniciar al terminar
winget install --id GitHub.cli -e
winget install --id Microsoft.VisualStudioCode -e
winget install --id PostgreSQL.PostgreSQL.17 -e    # OPCIONAL: usar mejor `docker compose exec db psql`
winget install --id Microsoft.OpenSSH.Beta -e      # si ssh/ssh-keygen no existen
```

Extensiones de VS Code: C# Dev Kit, Docker, GitHub Actions, EditorConfig, YAML, SQLTools (+ driver PostgreSQL).

### 0.2 Verificación

```powershell
git --version
dotnet --list-sdks        # debe listar 10.0.x
docker version
docker compose version
gh --version
ssh -V
wsl --status
```

Criterio: todos responden sin error y `docker run --rm hello-world` funciona.

### 0.3 Configuración

```powershell
# Identidad ya configurada (decisión D1): maomauro <maomauro.c@gmail.com>, GitHub por HTTPS.
git config --global init.defaultBranch main
git config --global core.autocrlf input
git config --global pull.rebase true
# El resto de la configuración está en docs/git-guia-tecnica.md, sección 3.

# La llave id_ed25519 ya existe; se usa para el VPS. gh ya está autenticado (HTTPS).
```

Crear una **segunda llave** solo para despliegue (fase 6): `ssh-keygen -t ed25519 -f $HOME\.ssh\gdb_deploy -C "gdb-deploy"` (sin passphrase, porque la usará GitHub Actions).

---

## Fase 1. Repositorio local

Hoy la carpeta **no es repositorio** (H3) y contiene `obj/` generado.

### 1.1 Archivos de base

Crear en la raíz:

- `global.json` fijando el SDK: `{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }` (ajustar a la versión que muestre `dotnet --version`).
- `.editorconfig` (indentación, `nullable`, estilo C#, fin de línea LF).
- `.gitattributes`: `* text=auto eol=lf` y `*.sql text eol=lf`, `*.sh text eol=lf`.
- `Directory.Build.props`: `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors=true`, `AnalysisMode=Recommended`.
- `Directory.Packages.props` con `ManagePackageVersionsCentrally=true` y **versiones exactas** de CoreWCF, Npgsql, etc. (B1). Obtener las actuales con `dotnet list package` y quitar `Version` de los `.csproj`.
- `.github/CODEOWNERS`, `.github/pull_request_template.md`.
- `CLAUDE.md` en la raíz (ya creado): léelo y ajusta lo que no corresponda a tu criterio. Revisarlo al cerrar cada fase.
- `docs/git-guia-tecnica.md` (ya creado): fija el modelo de ramas, los nombres y el formato de commits que se usan en el resto del plan.

### 1.2 Revisar `.gitignore`

Debe excluir: `bin/`, `obj/`, `.vs/`, `.env`, `*.user`, `TestResults/`, `*.log`. Ejecutar `dotnet clean` y borrar `obj/` antes del primer commit.

### 1.3 Inicializar y primer commit

```powershell
cd c:\dev\GrupoDigitalBank-CRUD
git init
git add .
git status                      # verificar: sin .env, sin obj/, sin bin/
git commit -m "chore: estado inicial del proyecto"
git switch -c chore/base-repositorio
```

### 1.4 Estrategia de ramas y commits

Detalle completo, con ramas locales y remotas, en [git-guia-tecnica.md](git-guia-tecnica.md). Resumen:

- `main` = producción. Todo cambio por rama corta (`feat/…`, `fix/…`, `chore/…`) + Pull Request.
- Commits en formato Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `ci:`).
- Releases con tags `vMAJOR.MINOR.PATCH`; **el despliegue a producción se dispara al hacer merge a `main`** (o por tag, si prefieres controlarlo; ver fase 7).
- La tabla "Ramas previstas por el plan de trabajo" de la guía asigna una rama a cada hallazgo y fase.

### 1.5 Criterio de salida

`git log` con el commit base, `git status` limpio, `CLAUDE.md` y guía de Git en el repo, y la primera rama de trabajo creada.

---

## Fase 2. Ejecutar en local

### 2.1 Con Docker (réplica de producción)

```powershell
copy .env.example .env
# editar .env: DOMINIO=localhost, contraseña propia
docker compose up -d --build
docker compose ps
docker compose logs -f negocios
```

Abrir `https://localhost` (aceptar el certificado interno de Caddy). Probar: registrar, consultar, modificar, eliminar y un error de validación (fecha futura, nombre vacío).

### 2.2 Sin Docker (ciclo de desarrollo rápido)

```powershell
docker run -d --name gdb-pg -e POSTGRES_PASSWORD=dev -e POSTGRES_DB=usuarios_db -p 5432:5432 postgres:17-alpine
psql -h localhost -U postgres -d usuarios_db -f db\001_esquema_usuario.sql
# terminal 1
dotnet user-secrets --project src\GrupoDigitalBank.Negocios set "ConnectionStrings:Postgres" "Host=localhost;Database=usuarios_db;Username=postgres;Password=dev"
dotnet run --project src\GrupoDigitalBank.Negocios
# terminal 2
dotnet run --project src\GrupoDigitalBank.Presentacion
```

Los secretos van en `user-secrets`, no en `appsettings.json` (B5).

### 2.3 Primer build real

El diagnóstico no pudo compilar. Ejecutar `dotnet build -warnaserror` y `dotnet format --verify-no-changes`; corregir lo que aparezca antes de la fase 3.

Nota: `.env.example` usa `POSTGRES_USER=usuarios_app` y Compose lo pasa como superusuario; eso se corrige en 3.4.

---

## Fase 3. Remediación del diagnóstico

Una rama y un PR por ítem. Orden por riesgo.

### 3.1 H1 · XSS en el botón Eliminar (alta)

- `Views/Usuarios/Consultar.cshtml`: quitar `onsubmit="return confirm('…@u.Nombre…')"`. Usar `<form data-confirmar="¿Eliminar este usuario?">` y un único listener en `wwwroot/js/site.js` que lea el atributo (texto genérico, o `data-nombre` leído con `textContent`).
- Prueba: usuario con nombre `x');alert(1);//` → la grilla carga sin ejecutar código.
- Añadir CSP en Caddy (3.8) que prohíba `script-src 'unsafe-inline'`; esto es defensa en profundidad y obliga a no tener handlers inline.

### 3.2 H2 · Autenticación mínima (alta)

Opción inmediata, sin código: Basic Auth en Caddy.

```caddyfile
{$DOMINIO} {
	basic_auth {
		{$AUTH_USUARIO} {$AUTH_HASH}
	}
	...
}
```

Generar el hash: `docker run --rm caddy:2-alpine caddy hash-password --plaintext 'clave'`. Escapar `$` como `$$` si va en `.env` de Compose. Añadir `AUTH_USUARIO` y `AUTH_HASH` a `.env.example`. Excluir `/health` del auth para los healthchecks externos.

Mejora posterior (fuera de este plan): ASP.NET Core Identity o proveedor OIDC. Registrar la decisión en un ADR.

### 3.3 H3 · Git (alta)

Cubierto en fase 1.

### 3.4 H4 · Rol de aplicación en PostgreSQL (media)

Nuevo `db/002_rol_aplicacion.sql`:

- `CREATE ROLE usuarios_app LOGIN PASSWORD :'app_pass'` (la contraseña se inyecta, no se versiona).
- `REVOKE ALL ON SCHEMA public FROM PUBLIC;`
- `GRANT USAGE ON SCHEMA public TO usuarios_app;`
- `GRANT EXECUTE ON FUNCTION fn_usuario_crud(...) TO usuarios_app;`
- Si la función no es `SECURITY DEFINER`, conceder `SELECT/INSERT/UPDATE/DELETE` solo sobre `usuario`; mejor: declarar la función `SECURITY DEFINER SET search_path = public` y no conceder nada sobre la tabla.

Compose: `POSTGRES_USER` pasa a ser el **propietario/migrador** (`usuarios_admin`) y `negocios` se conecta con `APP_DB_USER`/`APP_DB_PASSWORD`. Variables nuevas en `.env.example`.

### 3.5 H5 · Migraciones y backups (media)

Reemplazar `docker-entrypoint-initdb.d` por un servicio one-shot:

- `db/migrate.sh`: crea `schema_migrations(version text primary key, aplicada_en timestamptz)`, recorre `db/[0-9]*.sql` en orden, aplica con `psql -v ON_ERROR_STOP=1 --single-transaction` los que no estén registrados y los registra.
- Servicio `migrador` en Compose (`postgres:17-alpine`, monta `./db`, `depends_on: db healthy`, `restart: "no"`), y `negocios` depende de `migrador: condition: service_completed_successfully`.
- Regla: **los scripts aplicados nunca se editan**; cualquier cambio es un `00N_…sql` nuevo.
- Backups: ver fase 9.

### 3.6 H6 · Claves de Data Protection (media)

En `Presentacion/Program.cs`: `AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys")).SetApplicationName("GrupoDigitalBank")`. En Compose, volumen `dpkeys:/keys` para `web`; en el Dockerfile crear `/keys` con propietario del usuario no root.

### 3.7 H7 · Pruebas (media)

Crear `tests/`:

| Proyecto | Contenido | Herramientas |
|---|---|---|
| `GrupoDigitalBank.Negocios.Tests` | Unitarias de `UsuarioValidador` y `UsuarioService` (repositorio simulado) | xUnit, FluentAssertions o Shouldly, NSubstitute |
| `GrupoDigitalBank.Negocios.IntegrationTests` | Repositorio contra PostgreSQL real, aplicando los scripts SQL | Testcontainers for .NET |
| `GrupoDigitalBank.Presentacion.Tests` | Controlador con `IUsuarioClient` simulado; pruebas de vista (sin XSS) | xUnit, `WebApplicationFactory` |

Casos mínimos: crear válido; nombre vacío/largo; fecha futura/muy antigua; sexo inválido; modificar o eliminar inexistente; operación inválida en la función; fallo del servicio SOAP mostrado como error amigable; regresión de H1.

Meta: ≥ 80 % de cobertura en lógica de Negocios, medida con `coverlet`.

### 3.8 H8 y baja prioridad

| Ítem | Acción |
|---|---|
| H8 `docs/` vacío | Crear `docs/arquitectura.md` (C4 contexto y contenedores en Mermaid), `docs/adr/0001-corewcf.md`, `0002-sin-orm.md`, `0003-contrato-soap-duplicado.md` (B2), `0004-autenticacion.md`; `runbook.md` (fase 9). Quitar `docs` de `.dockerignore` solo si algo lo necesita en la imagen (no). |
| B1 versiones flotantes | `Directory.Packages.props` (fase 1) |
| B2 contrato duplicado | ADR; añadir prueba que compare el WSDL de Negocios con `ContratoServicio.cs` (opcional) |
| B3 healthchecks | `healthcheck` en `negocios` y `web` (`wget -qO- http://localhost:8080/health`; si la imagen no trae wget/curl, instalarlo en el Dockerfile); `depends_on: condition: service_healthy` |
| B4 cabeceras | En Caddy: `Strict-Transport-Security "max-age=31536000"`, `X-Content-Type-Options nosniff`, `X-Frame-Options DENY`, `Referrer-Policy no-referrer`, `Content-Security-Policy "default-src 'self'; object-src 'none'; frame-ancestors 'none'"`, `-Server` |
| B5 contraseña en `appsettings.json` | Dejar la cadena vacía; fallar al arrancar si falta |
| B6 zona horaria | Usar `TimeProvider` inyectable y zona `America/Bogota` para "hoy"; en la BD, `(now() AT TIME ZONE 'America/Bogota')::date`. Pruebas con `FakeTimeProvider` |
| B7 paginación | Diferir; anotar en backlog |
| B8 WSDL | `if (env.IsDevelopment())` para `HttpGetEnabled`/metadata |

### 3.9 Criterio de salida de la fase

`dotnet build -warnaserror`, `dotnet test` y `docker compose up` verdes en local; todos los hallazgos de alta y media cerrados.

---

## Fase 4. Repositorio remoto (GitHub)

```powershell
gh repo create GrupoDigitalBank-CRUD --private --source . --remote origin --push
git push -u origin chore/base-repositorio
git branch -vv ; git branch -r      # confirmar que main y la rama de trabajo rastrean a origin
```

Cómo se relacionan las ramas locales y remotas, y el flujo diario, están en [git-guia-tecnica.md](git-guia-tecnica.md). Activar además *Automatically delete head branches* y limitar los merges a squash.

Configurar en *Settings*:

1. **Branches → protección de `main`:** exigir PR, 1 aprobación (o ninguna si trabajas solo, pero mantener el PR), checks `ci` obligatorios, rama actualizada, sin force-push, historial lineal.
2. **Environments → `production`:** revisores requeridos (tú) y solo desde `main`.
3. **Secrets (environment `production`):**

   | Secreto | Contenido |
   |---|---|
   | `VPS_HOST` | IP o dominio del VPS |
   | `VPS_PORT` | Puerto SSH (22 o el elegido) |
   | `VPS_USER` | `deploy` |
   | `VPS_SSH_KEY` | Llave privada `gdb_deploy` |
   | `VPS_KNOWN_HOSTS` | Salida de `ssh-keyscan -p <puerto> <host>` |

   Las contraseñas de la app **no** van en GitHub: viven en `/opt/gdb/.env` del VPS.
4. **Code security:** activar Dependabot alerts/updates, secret scanning y push protection.
5. **Actions → General:** permisos de `GITHUB_TOKEN` en solo lectura por defecto.
6. **Packages:** tras la primera publicación, vincular el paquete al repo y dejarlo privado.

`.github/dependabot.yml`: ecosistemas `nuget`, `docker`, `github-actions`, semanal.

---

## Fase 5. Integración continua

`.github/workflows/ci.yml`: se ejecuta en `pull_request` y en `push` a `main`.

```yaml
name: ci
on:
  pull_request:
  push:
    branches: [main]
permissions:
  contents: read
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
jobs:
  ci:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { global-json-file: global.json }
      - run: dotnet restore --locked-mode        # exige packages.lock.json si se activa
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build -c Release --no-restore -warnaserror
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
      - uses: actions/upload-artifact@v4
        with: { name: cobertura, path: TestResults }
      - run: dotnet list package --vulnerable --include-transitive | tee vuln.txt; ! grep -q "has the following vulnerable" vuln.txt
  docker:
    runs-on: ubuntu-latest
    needs: ci
    steps:
      - uses: actions/checkout@v4
      - run: docker compose -f docker-compose.yml -f docker-compose.prod.yml config -q
      - run: docker build -f src/GrupoDigitalBank.Negocios/Dockerfile -t negocios:ci .
      - run: docker build -f src/GrupoDigitalBank.Presentacion/Dockerfile -t web:ci .
      - uses: aquasecurity/trivy-action@0.28.0
        with: { image-ref: web:ci, severity: 'CRITICAL,HIGH', exit-code: '1', ignore-unfixed: true }
```

Extras recomendados: `hadolint` sobre los Dockerfile, `shellcheck` sobre `db/migrate.sh`, y CodeQL (`github/codeql-action`) para C#.

Criterio: un PR con una prueba rota queda bloqueado; uno limpio se puede fusionar. Marcar `ci` y `docker` como checks requeridos en la protección de `main`.

---

## Fase 6. Preparar el VPS

### 6.1 DNS

Registro `A` de `usuarios.midominio.com` → IP del VPS (y `AAAA` si hay IPv6). Verificar con `nslookup usuarios.midominio.com` antes del primer arranque; Let's Encrypt falla si no resuelve.

### 6.2 Acceso y usuario de despliegue (como root)

```bash
apt update && apt -y upgrade
adduser --disabled-password --gecos "" deploy
usermod -aG sudo deploy        # solo si necesitas sudo; si no, omitir
mkdir -p /home/deploy/.ssh && chmod 700 /home/deploy/.ssh
# pegar la clave pública personal Y la de despliegue (gdb_deploy.pub)
nano /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys && chown -R deploy:deploy /home/deploy/.ssh
```

Verificar `ssh deploy@<ip>` **desde otra terminal** antes de endurecer.

### 6.3 Endurecimiento SSH

En `/etc/ssh/sshd_config.d/99-hardening.conf`:

```
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
AllowUsers deploy
```

`sshd -t && systemctl reload ssh`. Mantener una sesión abierta mientras se prueba.

### 6.4 Firewall y protección

```bash
apt -y install ufw fail2ban unattended-upgrades
ufw default deny incoming && ufw default allow outgoing
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp && ufw allow 443/udp
ufw enable
systemctl enable --now fail2ban
dpkg-reconfigure -plow unattended-upgrades
timedatectl set-timezone America/Bogota   # o dejar UTC y documentarlo
```

Si el proveedor tiene firewall de nube, replicar las mismas reglas allí. Nota: Docker publica puertos saltándose `ufw`; por eso en Compose **solo** el proxy publica puertos (ya es así) y `db` nunca lleva `ports:`.

### 6.5 Docker

```bash
curl -fsSL https://get.docker.com | sh
usermod -aG docker deploy
```

Limitar logs en `/etc/docker/daemon.json`: `{"log-driver":"json-file","log-opts":{"max-size":"10m","max-file":"3"}}` y `systemctl restart docker`. Comprobar `docker compose version` como `deploy`.

### 6.6 Estructura de la aplicación en el servidor

```bash
sudo mkdir -p /opt/gdb/{backups,db} && sudo chown -R deploy:deploy /opt/gdb
```

`/opt/gdb/.env` (permisos `600`, **solo en el servidor**):

```
DOMINIO=usuarios.midominio.com
POSTGRES_DB=usuarios_db
POSTGRES_USER=usuarios_admin
POSTGRES_PASSWORD=<generada con: openssl rand -base64 32>
APP_DB_USER=usuarios_app
APP_DB_PASSWORD=<generada>
AUTH_USUARIO=admin
AUTH_HASH=<hash de caddy, con $ escapado como $$>
IMAGE_TAG=latest
GITHUB_REPOSITORY=<usuario>/grupodigitalbank-crud
```

Login a GHCR desde el VPS con un PAT de **solo lectura de paquetes** (`read:packages`):
`echo $PAT | docker login ghcr.io -u <usuario> --password-stdin`.

---

## Fase 7. Entrega continua

### 7.1 `docker-compose.prod.yml` (override)

Sustituye `build:` por imágenes versionadas:

```yaml
services:
  negocios:
    image: ghcr.io/${GITHUB_REPOSITORY}/negocios:${IMAGE_TAG}
    build: !reset null
  web:
    image: ghcr.io/${GITHUB_REPOSITORY}/web:${IMAGE_TAG}
    build: !reset null
```

(`!reset` requiere Compose ≥ 2.24; si no, mantener `docker-compose.yml` con `build` solo para desarrollo y usar un `docker-compose.prod.yml` completo.) Añadir `restart: unless-stopped`, límites de memoria (`mem_limit`) y `read_only: true` + `tmpfs: /tmp` en `web` y `negocios` si arrancan bien así.

### 7.2 Workflow de despliegue `.github/workflows/deploy.yml`

Disparo: `push` a `main` tras CI verde (`workflow_run`) o `push` de tag `v*`.

Pasos:

1. **build-push:** `docker/login-action` a GHCR con `GITHUB_TOKEN` (`permissions: packages: write`), `docker/build-push-action` para `negocios` y `web`, etiquetas `sha-<corto>` y `latest` (y `vX.Y.Z` en tags), caché `type=gha`. Opcional: `provenance` y SBOM.
2. **deploy** (environment `production`, `needs: build-push`):
   - Configurar `~/.ssh` con `VPS_SSH_KEY` y `VPS_KNOWN_HOSTS` (nunca `StrictHostKeyChecking=no`).
   - Copiar con `scp`/`rsync`: `docker-compose.yml`, `docker-compose.prod.yml`, `Caddyfile`, `db/`.
   - Por SSH ejecutar un script `deploy/desplegar.sh`:
     ```bash
     set -euo pipefail
     cd /opt/gdb
     export IMAGE_TAG="$1"
     PREVIO=$(grep ^IMAGE_TAG= .env | cut -d= -f2)
     sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$IMAGE_TAG/" .env
     docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
     docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans --wait --wait-timeout 120 \
       || { echo "Fallo; rollback a $PREVIO"; sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$PREVIO/" .env; \
            docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --wait; exit 1; }
     docker image prune -f
     ```
   - **Pre-despliegue:** ejecutar el backup (fase 9) antes de aplicar migraciones nuevas.
3. **smoke test:** `curl -fsS --retry 10 --retry-delay 5 https://$DOMINIO/health` (con `/health` fuera del Basic Auth) y verificación de que la cabecera HSTS está presente.
4. Etiquetar con `concurrency: deploy-production` y `cancel-in-progress: false` para que no se crucen despliegues.

### 7.3 Rollback

- Automático si `up --wait` falla (script anterior).
- Manual: re-ejecutar el workflow con una tag/SHA anterior (`workflow_dispatch` con input `tag`) o, en el VPS, cambiar `IMAGE_TAG` y `docker compose up -d`.
- Migraciones destructivas: se revierten restaurando el backup previo, por eso el paso de backup es obligatorio antes de migrar. Diseñar los cambios de esquema como *expand/contract* (compatibles hacia atrás durante un despliegue).

---

## Fase 8. Primera publicación

Checklist en orden:

1. DNS resolviendo al VPS (`nslookup`).
2. VPS endurecido y `/opt/gdb/.env` completo (fase 6).
3. Secretos en GitHub y environment `production` creado (fase 4).
4. Merge del PR de remediación a `main`; CI verde.
5. Ejecutar el deploy (automático o `workflow_dispatch`). Primer arranque: Caddy obtiene el certificado (puede tardar ~1 min).
6. En el VPS: `docker compose ps` (todo `healthy`), `docker compose logs proxy | tail` sin errores ACME.
7. Verificación externa:
   - `https://<dominio>` pide usuario y clave (Basic Auth).
   - Registrar → consultar → modificar → eliminar.
   - `http://<dominio>` redirige a HTTPS.
   - `nmap -p 1-10000 <ip>` solo muestra 22, 80, 443; el 5432 **no** responde.
   - securityheaders.com y SSL Labs en nota A.
   - Probar el nombre `x');alert(1);//`: no ejecuta nada.
8. Reiniciar el VPS (`reboot`) y confirmar que todo vuelve solo (`restart: unless-stopped` + Docker habilitado).
9. Crear tag `v1.0.0` y release en GitHub con notas.

---

## Fase 9. Operación

### 9.1 Backups de PostgreSQL

`/opt/gdb/backup.sh` (ejecutado por cron del usuario `deploy`, diario 02:30):

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /opt/gdb; source .env
F=backups/usuarios_$(date +%F_%H%M).sql.gz
docker compose exec -T db pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner | gzip > "$F"
find backups -name 'usuarios_*.sql.gz' -mtime +14 -delete
```

- Cron: `30 2 * * * /opt/gdb/backup.sh >> /opt/gdb/backups/backup.log 2>&1`.
- Copia **fuera del servidor** (regla 3-2-1): `rclone` hacia un bucket (Backblaze B2, S3, Drive) o `rsync` a otra máquina.
- **Probar la restauración** al menos una vez en local: `gunzip -c backup.sql.gz | docker exec -i <db> psql -U … -d …`. Un backup sin restauración probada no cuenta.

### 9.2 Monitoreo

- Uptime externo (UptimeRobot / Better Stack) sobre `https://<dominio>/health`, alerta a tu correo.
- Alertas de espacio en disco (`df`) vía cron o agente del proveedor.
- Logs: `docker compose logs --since 1h`; rotación ya limitada en `daemon.json`.
- Opcional: Grafana Cloud/Netdata, y Dozzle para ver logs vía web (tras el Basic Auth).

### 9.3 Mantenimiento

| Tarea | Frecuencia |
|---|---|
| Revisar y fusionar PR de Dependabot | Semanal |
| `apt upgrade` + reinicio si el kernel cambia | Automático (`unattended-upgrades`) + revisión mensual |
| Rotar contraseñas de BD, llave de despliegue y PAT de GHCR | Cada 6 meses |
| Probar restauración de backup | Trimestral |
| Revisar certificado | Caddy renueva solo; la alerta de uptime avisa si falla |
| Actualizar imagen base de Postgres (menor) | Mensual; mayores con `pg_dump`/restauración |

### 9.4 Runbook (`docs/runbook.md`)

Documentar: cómo desplegar, hacer rollback, restaurar backup, rotar secretos, qué hacer si el certificado no se emite, si el disco se llena y si el VPS no responde (consola del proveedor).

---

## Fase 10. Cierre y aceptación

0. `CLAUDE.md`: actualizar la sección "Estado conocido" y los comandos reales (tests, CI, despliegue).
1. README actualizado: requisitos, desarrollo local, pruebas, variables, despliegue, enlaces a ADRs y runbook; insignias de CI.
2. `docs/` con arquitectura, ADRs y runbook.
3. Actualizar el diagnóstico con una tabla "estado": cada hallazgo → PR que lo cierra.
4. Medir métricas DORA básicas (frecuencia de despliegue, tiempo de entrega por cambio) con el historial de Actions.

### Checklist final de aceptación

- [ ] Herramientas instaladas y verificadas
- [ ] Repo Git local con historial limpio y sin `obj/`, `bin/`, `.env`
- [ ] `CLAUDE.md` y `docs/git-guia-tecnica.md` presentes y vigentes
- [ ] Ramas locales y remotas alineadas (`git branch -vv` sin `gone`; ramas fusionadas borradas)
- [ ] Aplicación corre en local con y sin Docker
- [ ] H1–H8 cerrados; B1–B8 cerrados o registrados en backlog con motivo
- [ ] Pruebas unitarias e integración pasando; cobertura ≥ 80 % en Negocios
- [ ] Repo en GitHub, `main` protegida, secretos y environment configurados
- [ ] CI obligatorio en PR (build, format, tests, vulnerabilidades, Trivy)
- [ ] VPS: usuario sin root, SSH por llave, firewall, fail2ban, actualizaciones automáticas
- [ ] DNS + HTTPS válido con HSTS y cabeceras de seguridad
- [ ] Aplicación protegida con autenticación
- [ ] BD accesible solo desde la red interna y con rol de mínimo privilegio
- [ ] Despliegue automático con smoke test y rollback probado (provocar un fallo a propósito)
- [ ] Backup diario, copia externa y restauración probada
- [ ] Monitoreo externo activo y alerta recibida en una prueba
- [ ] Reinicio del VPS sin intervención manual
- [ ] Documentación y runbook completos

## Riesgos y dependencias

| Riesgo | Mitigación |
|---|---|
| El proyecto nunca se ha compilado | Fase 2.3 antes de cualquier cambio |
| Let's Encrypt limita emisiones si el DNS está mal | Verificar DNS; usar el CA de pruebas de Caddy (`acme_ca` staging) en el primer intento |
| Perder datos en una migración | Backup obligatorio antes del despliegue y scripts *expand/contract* |
| Llave de despliegue comprometida | Usuario `deploy` sin sudo, llave exclusiva, rotación semestral |
| Docker salta `ufw` | Ningún servicio salvo el proxy publica puertos |
| Un solo VPS (sin alta disponibilidad) | Aceptado a esta escala; backups externos permiten reconstruir en otro host con el runbook |

## Información que necesito de ti para ejecutar

1. Proveedor, SO y acceso (IP, usuario) del VPS.
2. Dominio/subdominio a usar y dónde está el DNS.
3. Nombre de usuario/organización de GitHub.
4. Si prefieres despliegue en cada merge a `main` o solo con tags.
5. Si Basic Auth basta como autenticación inicial o quieres login propio ya.
