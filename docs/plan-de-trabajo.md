# Plan de trabajo: de local a producción en VPS

Base: [diagnostico-2026-10-09.md](diagnostico/diagnostico-2026-10-09.md). Cada hallazgo (H1…H8 alta/media, B1…B8 baja) queda asignado a una fase. Ciclo de trabajo y decisiones: [ciclo-de-vida-desarrollo.md](ciclo-de-vida-desarrollo.md). Uso de Git: [git-guia-tecnica.md](git-guia-tecnica.md).

Leyenda de estado: ✅ hecho · 🔄 en curso · ⏳ pendiente.

## Supuestos y decisiones

| Tema | Decisión |
|---|---|
| Equipo local | Windows 11, PowerShell, Docker Desktop (D5: modo B para programar, modo A antes del PR) |
| Git | Identidad `maomauro <maomauro.c@gmail.com>`, GitHub por HTTPS (D1) |
| Remoto | GitHub **Free**, cuenta `maomauro`, repo **privado** (D2) |
| Flujo de ramas (D7) | `feature → develop → main → producción`. Feature = `feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/`; fusión a `develop` con squash y de `develop` a `main` con merge commit. El push a `main` despliega. Detalle en [git-guia-tecnica.md](git-guia-tecnica.md) |
| Tareas | GitHub Issues con etiquetas P0–P3 (D3) |
| Revisión | CI + checklist del PR, sin aprobación de terceros (D4). En plan Free no se puede imponer en el servidor: se sostiene con el hook y disciplina |
| Hook local | `pre-push` con `format` + `build`, y rechazo del push directo a `main` y `develop` (D6) |
| VPS (supuesto) | Ubuntu 24.04 LTS, acceso root inicial por SSH, IP pública fija |
| Dominio (supuesto) | Subdominio propio con registro A hacia el VPS |
| Registro de imágenes | GitHub Container Registry (`ghcr.io`) |
| Despliegue | GitHub Actions → SSH al VPS → `docker compose pull && up -d` |
| Migraciones | Scripts SQL numerados + contenedor one-shot con `psql` |
| Autenticación mínima | Basic Auth en Caddy; login propio es mejora futura |

## Resumen de fases

| # | Fase | Resultado | Estado | Esfuerzo |
|---|---|---|---|---|
| 0 | Herramientas locales | Máquina lista | ✅ | — |
| 1 | Repositorio local | Git inicializado, convenciones, commit base | ✅ | — |
| 2 | Repositorio remoto | GitHub con `main` y `develop`, ajustes de merge; barreras según 2.3 | 🔄 | 1 h |
| 3 | Ambiente local y primer build | Compose de desarrollo, build real, scripts de verificación | ⏳ | 3–4 h |
| 4 | Remediación del diagnóstico | Hallazgos corregidos con pruebas | ⏳ | 2–3 días |
| 5 | Integración continua | Build + tests + escaneo en cada PR | ⏳ | 3–4 h |
| 6 | Preparar el VPS | Servidor endurecido; GitHub conectado al servidor | ⏳ | 3–4 h |
| 7 | Entrega continua | Imágenes en GHCR y despliegue automático con rollback | ⏳ | 4–6 h |
| 8 | Primera publicación | Sistema en `https://<dominio>` | ⏳ | 1–2 h |
| 9 | Operación | Backups, monitoreo, runbook | ⏳ | 3–4 h |
| 10 | Cierre | Documentación y checklist de aceptación | ⏳ | 2 h |

Orden: 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10. La fase 6 (VPS, DNS) es independiente y puede adelantarse en paralelo a la 4 mientras se espera propagación de DNS. **Desde la fase 2 todo cambio entra por rama + PR** (ciclo de vida, etapas 1–7).

## Prioridades

- **P0, bloqueante:** sin esto no se puede avanzar ni publicar de forma segura.
- **P1, antes de producción:** debe estar listo antes del primer despliegue.
- **P2, primeras semanas en producción:** conviene cerrar pronto, pero no impide publicar.
- **P3, backlog:** mejora diferible.

| Prio | Ítem | Fase |
|---|---|---|
| P0 | Herramientas, `git init`, convenciones, commit base | 0, 1 ✅ |
| P0 | Repo remoto, rama `develop` por defecto, ajustes de merge y barreras (hook) | 2 |
| P0 | Compose de desarrollo, `.env` local y primer build real (nunca se ha compilado) | 3 |
| P0 | H1 XSS en Eliminar | 4.1 |
| P1 | H5 migraciones (antes que H4: H4 depende del migrador) | 4.4 |
| P1 | H4 rol de BD de mínimo privilegio | 4.5 |
| P1 | H2 autenticación (Basic Auth) | 4.2 |
| P1 | H6 claves de Data Protection | 4.6 |
| P1 | H7 pruebas mínimas (validador, servicio, repositorio, regresión H1) | 4.7 |
| P1 | B1 versiones fijas, B3 healthchecks, B4 cabeceras, B5 sin contraseña en `appsettings` | 3.6, 4.8 |
| P1 | CI | 5 |
| P1 | VPS endurecido, DNS, Docker, conexión con GitHub | 6 |
| P1 | CD con backup previo, smoke test y rollback | 7 |
| P1 | Primera publicación | 8 |
| P1 | Backup diario y restauración probada | 9.1 |
| P2 | Monitoreo externo, runbook, rotación de secretos | 9 |
| P2 | H8 arquitectura y ADRs, B6 zona horaria, B8 WSDL solo en desarrollo | 4.8 |
| P2 | Cobertura ≥ 80 %, Trivy, CodeQL, hadolint | 5 |
| P3 | B7 paginación, B2 prueba de contrato SOAP, login propio (Identity/OIDC), métricas DORA | 4.8, 10 |

Camino crítico: 2 → 3 → H1 → H5 → H4 → H2 → H6 → pruebas mínimas → CI → 6 → 7 → 8 → backup.

## Documentos de soporte

| Documento | Para qué | Estado |
|---|---|---|
| [CLAUDE.md](../CLAUDE.md) | Contexto del proyecto para Claude Code | ✅ se actualiza al cerrar cada fase |
| [ciclo-de-vida-desarrollo.md](ciclo-de-vida-desarrollo.md) | Ciclo local → GitHub, brechas, DoR/DoD, decisiones | ✅ |
| [git-guia-tecnica.md](git-guia-tecnica.md) | Ramas locales y remotas, flujo, protecciones | ✅ |
| [diagnostico-2026-10-09.md](diagnostico/diagnostico-2026-10-09.md) | Hallazgos de partida | ✅ |
| `arquitectura.md`, `adr/` | Decisiones y diagramas C4 | Fase 4.8 |
| `runbook.md` | Operación y recuperación | Fase 9.4 |

---

## Fase 0. Herramientas locales ✅

Verificado con [scripts/verificar-herramientas.ps1](../scripts/verificar-herramientas.ps1) (no instala ni modifica nada; devuelve código 1 si falta algo obligatorio):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1
```

Resultado del 2026-10-09: Git 2.53, .NET SDK 10.0.201, Docker Desktop 29.6.2 (Compose v5.3.1, daemon activo), GitHub CLI 2.92 autenticado, OpenSSH con llave `id_ed25519`, VS Code y WSL. Sin instalaciones pendientes.

Docker ejecuta el sistema; el SDK de .NET es la herramienta de desarrollo (`build`, `test`, `format`, `user-secrets`, depuración). `psql` local es opcional: se usa `docker compose exec db psql`.

Pendientes opcionales: extensiones de VS Code Docker, EditorConfig y SQLTools (se recomiendan en `.vscode/extensions.json`, fase 3.4). La segunda llave SSH, de despliegue (`gdb_deploy`), se crea en la fase 6.

Configuración global de Git aplicada (ver [git-guia-tecnica.md](git-guia-tecnica.md) §3): rama inicial `main`, `pull.rebase`, `fetch.prune`, `push.autoSetupRemote`, `rebase.autoStash`, `rerere`, `core.autocrlf=input`.

---

## Fase 1. Repositorio local ✅

Hecho:

- `git init -b main` y commit base `ce8dc86` directamente en `main` (única excepción a la regla de PR, porque aún no existe remoto).
- `.gitignore` reforzado, `.gitattributes` (LF), `global.json` (SDK 10, `rollForward: latestFeature`), `Directory.Build.props` (Nullable, ImplicitUsings, AnalysisLevel), `.editorconfig`.
- `.github/pull_request_template.md`, `.github/ISSUE_TEMPLATE/tarea.yml`, `.github/CODEOWNERS`.
- `CLAUDE.md`, guía de Git, ciclo de vida, plan, diagnóstico y script de verificación.
- H3 (no era repositorio Git) queda cerrado.

Diferido a propósito:

- `TreatWarningsAsErrors`: se activa en 3.2, tras el primer build limpio. Hasta entonces `-warnaserror` va en la línea de comandos.
- `Directory.Packages.props` (B1): se crea en 3.6, con las versiones que el primer build realmente resuelva.

---

## Fase 2. Repositorio remoto (GitHub) ⏳

Se hace **ahora**, antes de seguir desarrollando, para que todo cambio posterior viaje por rama + PR y quede respaldado fuera del equipo.

### 2.1 Crear y publicar ✅

Hecho el 2026-10-09: <https://github.com/maomauro/GrupoDigitalBank-CRUD> (privado), `main` publicada con el commit base `ce8dc86` y rastreando `origin/main`. Las etiquetas P0–P3, `hallazgo` y `urgente` ya están creadas.

```powershell
gh repo create GrupoDigitalBank-CRUD --private --source . --remote origin --push
git branch -vv ; git branch -r      # main debe rastrear origin/main
```

### 2.2 Rama `develop` y ajustes del repositorio (D7)

```powershell
git switch main; git pull
git switch -c develop
git push -u origin develop
gh repo edit maomauro/GrupoDigitalBank-CRUD --default-branch develop --enable-squash-merge --enable-merge-commit --enable-rebase-merge=false --delete-branch-on-merge=false
```

- `develop` es la rama por defecto: los PR apuntan a ella por omisión y `Closes #n` cierra el issue al fusionar en ella.
- Squash **y** merge commit habilitados (squash para feature → `develop`; merge commit para `develop` → `main`). Rebase deshabilitado.
- **Borrado automático de ramas desactivado**: con él GitHub borraría `develop` al fusionarla a `main`. Las ramas de feature se borran con `gh pr merge --squash --delete-branch`.
- `.github/dependabot.yml`: ecosistemas `nuget`, `docker` (una entrada por carpeta con Dockerfile) y `github-actions`, semanal, con `target-branch: develop`. Se publica por PR.
- Seguridad: Dependabot alerts; secret scanning y push protection solo si el plan lo permite.
- *Actions → General*: `GITHUB_TOKEN` en solo lectura por defecto.

### 2.3 Protección de `main` y `develop` (cuenta Free)

> **Verificado el 2026-10-09:** la API respondió `403 Upgrade to GitHub Pro or make this repository public`. La cuenta Free no admite protección de ramas, *rulesets* ni reglas de environments en repos privados.

Vigente (**opción A**): las reglas se sostienen con el hook `pre-push` (rechaza push directo a `main` y `develop`, fase 3.5), el CI visible en cada PR, el checklist del PR y los ajustes de merge. Es una barrera local: se salta con `--no-verify` o desde otro equipo. Detalle en la guía de Git, §10.

Si se quiere imponerlas en el servidor:

| Opción | Efecto | Costo |
|---|---|---|
| A. Disciplina + hook `pre-push` | Sin imposición en el servidor | Gratis; **vigente** |
| B. Repo público | Protección y environments disponibles; el código queda visible | Gratis; exige cero secretos en el historial y revisar `appsettings` |
| C. GitHub Pro | Protección completa en privado | Pago mensual |

Con B o C: PR obligatorio en `main` y `develop`, checks `ci` y `docker` obligatorios (se añaden en la fase 5), sin force-push ni borrado, historial lineal en `develop`, aplicar a administradores y proteger tags `v*`. Se registra la elección en `docs/adr/0005-proteccion-de-rama.md`.

### 2.4 Issues y primer flujo

- Crear un issue por hallazgo H1–H8 y por brecha pendiente del ciclo de vida (L6–L13), con su prioridad.
- Subir este ajuste de documentación como primer PR: rama `docs/ajuste-plan-trabajo` → `develop`, fusionado con squash.
- Primer release de documentación opcional: `develop` → `main` solo cuando haya algo desplegable (los cambios de documentación no requieren liberar).

### 2.5 Criterio de salida

`git branch -vv` sin ramas huérfanas; `main` y `develop` publicadas y `develop` como rama por defecto; el PR de documentación se abre contra `develop` y se fusiona con squash, y la rama de feature se borra.

---

## Fase 3. Ambiente local y primer build ⏳

Rama: `chore/ambiente-desarrollo`. Cierra L6–L11 del ciclo de vida.

### 3.1 Secretos locales (L8)

```powershell
copy .env.example .env
# editar .env: DOMINIO=localhost, contraseña de desarrollo propia (nunca la del VPS)
```

### 3.2 Primer build real (L9)

El proyecto nunca se ha compilado. Ejecutar:

```powershell
dotnet restore
dotnet build -warnaserror
dotnet format --verify-no-changes
```

Cada error o advertencia se corrige en commits propios y se reporta. Con el build limpio, activar `TreatWarningsAsErrors` en `Directory.Build.props`.

### 3.3 `docker-compose.dev.yml` (L6)

Override solo para desarrollo, **nunca se copia al VPS**:

- `db`: publica `127.0.0.1:5432:5432`.
- `proxy`: desactivado (`profiles: ["con-proxy"]`) o sin puertos; la web se publica en `http://localhost:8080`.
- `negocios` y `web`: variables de entorno de desarrollo (`ASPNETCORE_ENVIRONMENT=Development`).

Modo A, stack completo (validar antes del PR):

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
docker compose ps
docker compose logs -f negocios
```

Abrir `http://localhost:8080`. Probar registrar, consultar, modificar, eliminar y errores de validación (nombre vacío, fecha futura). Con el modo producción local (`https://localhost` con Caddy y `DOMINIO=localhost`) se prueba el proxy cuando haga falta.

Modo B, desarrollo diario (D5): solo la base en Docker, aplicaciones desde VS Code o terminal. Los `launchSettings.json` fijan Negocios en `http://localhost:5081` y Presentación en `http://localhost:5080` (el `Negocios:Url` por defecto ya apunta al 5081):

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db
# una sola vez: la cadena de conexión va en user-secrets, con los mismos datos del .env
dotnet user-secrets --project src\GrupoDigitalBank.Negocios set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=usuarios_db;Username=<usuario .env>;Password=<clave .env>"
dotnet watch --project src\GrupoDigitalBank.Negocios
dotnet watch --project src\GrupoDigitalBank.Presentacion      # otra terminal
```

Abrir `http://localhost:5080`. Los secretos solo se cargan en `Development` (lo fija el `launchSettings`) y el atributo `UserSecretsId` entra al ensamblado al compilar: si se cambia el `.csproj`, recompilar antes de usar `--no-build`.

El esquema (`db/001_*.sql`) lo aplica el contenedor al crear el volumen por primera vez. Consultas: `docker compose exec db psql -U <usuario> -d usuarios_db`.

Verificado el 2026-10-09 con el mismo flujo en ambos modos (registrar, consultar, modificar, eliminar y validaciones). Observaciones:

- B6 se reproduce en modo A: el contenedor corre en UTC y acepta como "hoy" una fecha que en Bogotá todavía es mañana. En modo B, Negocios usa la zona de Windows y la rechaza. Mismo código, resultado distinto según la zona del servidor.
- H1 se reproduce: el nombre `x');alert(1);//` queda almacenado y se emite dentro del `onsubmit`.
- Las imágenes de `web` y `negocios` no traen `wget` ni `curl`, así que el healthcheck de B3 requiere instalarlos o usar otra técnica.

### 3.4 Configuración de VS Code (L7)

`.vscode/launch.json` (depurar Negocios y Presentacion), `tasks.json` (build, watch, test, compose up/down) y `extensions.json` (C# Dev Kit, Docker, EditorConfig, YAML, GitHub Actions, SQLTools).

### 3.5 Verificación local y hook (L11, D6)

Hecho (issue #8):

- `scripts/verificar.ps1` (completa, ~30 s): restore → `dotnet format --verify-no-changes` → `dotnet build` (con `TreatWarningsAsErrors`) → `dotnet test`. Es el mismo contrato que CI (fase 5). Se detiene en el primer fallo y devuelve su código.
- `scripts/verificar.ps1 -Rapido` (~10 s): usa `dotnet format whitespace` (7 s en vez de 21 s) + build. El estilo y los analizadores ya los exige el build; el formato completo corre en la verificación completa y en CI.
- Hook versionado `.githooks/pre-push`:
  - **rechaza** el push directo a `main` y `develop`, incluido borrarlas;
  - al publicar una rama de trabajo ejecuta `verificar.ps1 -Rapido` y cancela el push si falla;
  - no verifica al borrar ramas ni al subir tags.
- Activación (una vez por clon): `git config core.hooksPath .githooks`. `scripts/verificar-herramientas.ps1` avisa si no está activo.
- Se probaron a mano los cinco escenarios (push a `develop`, a `main`, borrar `develop`, tag y rama de trabajo) y el fallo con formato roto.
- Límite: se salta con `git push --no-verify` o desde otro equipo (ver guía de Git, §10).

### 3.6 Esqueleto de pruebas y versiones fijas (L10, B1)

- Crear `tests/` con los tres proyectos de 4.7 (vacíos, solo compilan) y añadirlos a `GrupoDigitalBank.slnx`.
- `Directory.Packages.props` con `ManagePackageVersionsCentrally=true` y las versiones exactas resueltas por el build; quitar `Version` de los `.csproj`.
- Ajustar los Dockerfile para copiar `global.json`, `Directory.Build.props` y `Directory.Packages.props` antes del `restore` (si no, el build en Docker ignora esas reglas).

### 3.7 Criterio de salida

`scripts/verificar.ps1` en verde, el stack arranca en modo A y funciona de punta a punta, modo B depurable con F5, hook activo. Uno o varios PR pequeños llevan esto a `develop` (la fase 3 no se libera a `main` hasta que haya algo desplegable).

---

## Fase 4. Remediación del diagnóstico ⏳

Una rama y un PR por ítem, cada uno con su issue. Ramas en la tabla de [git-guia-tecnica.md](git-guia-tecnica.md). Orden: 4.1 → 4.4 → 4.5 → 4.2 → 4.6 → 4.7 → 4.8.

### 4.1 H1 · XSS en el botón Eliminar (alta, P0)

- `Views/Usuarios/Consultar.cshtml`: quitar `onsubmit="return confirm('…@u.Nombre…')"`. Usar `<form data-confirmar="¿Eliminar este usuario?">` y un único listener en `wwwroot/js/site.js` (texto genérico, o `data-nombre` leído como texto, nunca interpolado en JS).
- Prueba de regresión: usuario `x');alert(1);//` → la grilla carga sin ejecutar código.
- Defensa en profundidad: CSP en Caddy (4.8) sin `'unsafe-inline'` para scripts; confirmar que el layout no usa scripts ni estilos en línea que la CSP bloquearía.

### 4.2 H2 · Autenticación mínima (alta, P1)

Basic Auth en Caddy, sin código:

```caddyfile
{$DOMINIO} {
	encode gzip
	@salud path /health
	handle @salud {
		reverse_proxy web:8080
	}
	handle {
		basic_auth {
			{$AUTH_USUARIO} {$AUTH_HASH}
		}
		reverse_proxy web:8080
	}
}
```

Hash: `docker run --rm caddy:2-alpine caddy hash-password --plaintext 'clave'`. En `.env` se escribe entre comillas simples y con `$` duplicado como `$$` (Compose interpola `$`). Añadir `AUTH_USUARIO` y `AUTH_HASH` a `.env.example` y pasarlos al servicio `proxy` en Compose. `/health` queda fuera para los monitores externos. Login propio (Identity/OIDC): fuera de este plan, ADR `0004-autenticacion`.

### 4.3 H3 · Git ✅

Cerrado en la fase 1.

### 4.4 H5 · Migraciones (media, P1) — va antes que H4

Reemplazar `docker-entrypoint-initdb.d` por un servicio one-shot:

- `db/migrate.sh`: crea `schema_migrations(version text primary key, aplicada_en timestamptz default now())`, recorre `db/[0-9]*.sql` en orden y aplica con `psql -v ON_ERROR_STOP=1 --single-transaction` los que falten, registrándolos. Pasa las variables necesarias a `psql` con `-v` (p. ej. `-v app_pass="$APP_DB_PASSWORD"` para H4).
- Servicio `migrador` en Compose: `postgres:17-alpine`, monta `./db`, conecta como `POSTGRES_USER` (propietario), `depends_on: db: service_healthy`, `restart: "no"`. `negocios` depende de `migrador: condition: service_completed_successfully`.
- Quitar el montaje de `./db` en `docker-entrypoint-initdb.d`.
- Regla: **los scripts aplicados nunca se editan**; todo cambio es un `00N_…sql` nuevo. Si dos ramas crean el mismo número, la segunda en fusionarse renumera.
- Al adoptar el migrador, el volumen de desarrollo existente no tiene `schema_migrations`: recrearlo con `down -v` (en local no hay datos reales). En producción se adopta antes del primer despliegue, por lo que no hay datos que migrar.
- `shellcheck` sobre `migrate.sh` en CI.

### 4.5 H4 · Rol de aplicación en PostgreSQL (media, P1) — depende de 4.4

Nuevo `db/002_rol_aplicacion.sql`, aplicado por el migrador:

- `CREATE ROLE usuarios_app LOGIN PASSWORD :'app_pass'` (idempotente; contraseña inyectada por `-v`, no versionada).
- `REVOKE ALL ON SCHEMA public FROM PUBLIC; GRANT USAGE ON SCHEMA public TO usuarios_app;`
- `GRANT EXECUTE ON FUNCTION fn_usuario_crud(...) TO usuarios_app;`
- Declarar la función `SECURITY DEFINER SET search_path = public` (script nuevo; no se edita el `001`) para no conceder nada sobre la tabla; si no se hace, conceder `SELECT/INSERT/UPDATE/DELETE` solo sobre `usuario`.

Compose y `.env.example`: `POSTGRES_USER=usuarios_admin` (propietario/migrador) y `negocios` se conecta con `APP_DB_USER` / `APP_DB_PASSWORD`. Añadir las variables al `.env` local y documentar. Prueba: con el rol de aplicación, `DROP TABLE usuario` debe fallar.

### 4.6 H6 · Claves de Data Protection (media, P1)

`Presentacion/Program.cs`: `AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys")).SetApplicationName("GrupoDigitalBank")`. Compose: volumen `dpkeys:/keys` en `web`. Dockerfile: crear `/keys` con propietario del usuario no root.

### 4.7 H7 · Pruebas (media, P1)

| Proyecto | Contenido | Herramientas |
|---|---|---|
| `GrupoDigitalBank.Negocios.Tests` | Unitarias de `UsuarioValidador` y `UsuarioService` (repositorio simulado) | xUnit, Shouldly o FluentAssertions, NSubstitute |
| `GrupoDigitalBank.Negocios.IntegrationTests` | Repositorio contra PostgreSQL real aplicando los scripts de `db/` | Testcontainers for .NET |
| `GrupoDigitalBank.Presentacion.Tests` | Controlador con `IUsuarioClient` simulado; vistas (regresión H1) | xUnit, `WebApplicationFactory` |

Casos mínimos: crear válido; nombre vacío o largo; fecha futura o muy antigua; sexo inválido; modificar o eliminar inexistente; operación inválida en la función; fallo del servicio SOAP mostrado como error amigable; regresión H1; el rol de aplicación no puede borrar la tabla. Meta: ≥ 80 % de cobertura en la lógica de Negocios (`coverlet`).

### 4.8 H8 y baja prioridad

| Ítem | Acción |
|---|---|
| H8 `docs/` vacío | `docs/arquitectura.md` (C4 en Mermaid), `docs/adr/0001-corewcf.md`, `0002-sin-orm.md`, `0003-contrato-soap-duplicado.md` (B2), `0004-autenticacion.md`, `0005-proteccion-de-rama.md` (si aplica 2.3). `.dockerignore` sigue excluyendo `docs/` |
| B1 versiones flotantes | Hecho en 3.6 |
| B2 contrato duplicado | ADR; prueba opcional que compare el WSDL de Negocios con `ContratoServicio.cs` |
| B3 healthchecks | `healthcheck` en `negocios` y `web` sobre `/health` (instalar `wget` o `curl` en la imagen si no existe); `depends_on` con `service_healthy` |
| B4 cabeceras | Caddy: `Strict-Transport-Security "max-age=31536000"`, `X-Content-Type-Options nosniff`, `X-Frame-Options DENY`, `Referrer-Policy no-referrer`, `Content-Security-Policy "default-src 'self'; object-src 'none'; frame-ancestors 'none'"`, `-Server` |
| B5 contraseña en `appsettings.json` | Cadena vacía; fallar al arrancar si falta la cadena de conexión |
| B6 zona horaria | `TimeProvider` inyectable con `America/Bogota`; en la BD, `(now() AT TIME ZONE 'America/Bogota')::date`; pruebas con `FakeTimeProvider` |
| B7 paginación | Diferir; anotar en backlog |
| B8 WSDL | Metadata (`HttpGetEnabled`) solo en `IsDevelopment()` |

### 4.9 Criterio de salida

`scripts/verificar.ps1` y el stack en Docker verdes; H1–H8 de alta y media cerrados con su PR; B1–B8 cerrados o en backlog con motivo.

---

## Fase 5. Integración continua ⏳

Rama `ci/integracion-continua`. `.github/workflows/ci.yml` en `pull_request` hacia `develop` y `main`, y en `push` a ambas. Ejecuta lo mismo que `scripts/verificar.ps1`.

```yaml
name: ci
on:
  pull_request:
    branches: [develop, main]
  push:
    branches: [develop, main]
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
      - run: dotnet restore
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build -c Release --no-restore -warnaserror
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
      - uses: actions/upload-artifact@v4
        with: { name: cobertura, path: TestResults }
      - run: |
          dotnet list package --vulnerable --include-transitive | tee vuln.txt
          ! grep -q "has the following vulnerable" vuln.txt
  docker:
    runs-on: ubuntu-latest
    needs: ci
    steps:
      - uses: actions/checkout@v4
      - run: cp .env.example .env && docker compose config -q
      - run: docker build -f src/GrupoDigitalBank.Negocios/Dockerfile -t negocios:ci .
      - run: docker build -f src/GrupoDigitalBank.Presentacion/Dockerfile -t web:ci .
      - uses: aquasecurity/trivy-action@0.28.0
        with: { image-ref: negocios:ci, severity: 'CRITICAL,HIGH', exit-code: '1', ignore-unfixed: true }
      - uses: aquasecurity/trivy-action@0.28.0
        with: { image-ref: web:ci, severity: 'CRITICAL,HIGH', exit-code: '1', ignore-unfixed: true }
```

La validación de `docker-compose.prod.yml` se añade al `docker` job en la fase 7, cuando ese archivo exista.

Extras: `hadolint` sobre los Dockerfile, `shellcheck` sobre `db/migrate.sh`, CodeQL para C#.

Criterio: un PR con una prueba rota se ve en rojo y no se fusiona (por disciplina en plan Free; bloqueado si 2.3 pasa a B o C). Si aplica B o C, se marcan `ci` y `docker` como checks obligatorios en `develop` y `main`.

---

## Fase 6. Preparar el VPS ⏳

### 6.1 DNS

Registro `A` de `usuarios.midominio.com` → IP del VPS (y `AAAA` si hay IPv6). Verificar con `nslookup` antes del primer arranque; Let's Encrypt falla si no resuelve.

### 6.2 Acceso y usuario de despliegue (como root)

```bash
apt update && apt -y upgrade
adduser --disabled-password --gecos "" deploy
mkdir -p /home/deploy/.ssh && chmod 700 /home/deploy/.ssh
# pegar la clave pública personal y la de despliegue (gdb_deploy.pub)
nano /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys && chown -R deploy:deploy /home/deploy/.ssh
mkdir -p /opt/gdb/{backups,db} && chown -R deploy:deploy /opt/gdb
```

Antes: en el equipo local, `ssh-keygen -t ed25519 -f $HOME\.ssh\gdb_deploy -C "gdb-deploy"` (sin passphrase, porque la usará GitHub Actions). Verificar `ssh deploy@<ip>` **desde otra terminal** antes de endurecer. El usuario `deploy` no tiene `sudo`.

### 6.3 Endurecimiento SSH

`/etc/ssh/sshd_config.d/99-hardening.conf`:

```
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
AllowUsers deploy
```

`sshd -t && systemctl reload ssh`, con una sesión abierta mientras se prueba.

### 6.4 Firewall y protección

```bash
apt -y install ufw fail2ban unattended-upgrades
ufw default deny incoming && ufw default allow outgoing
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp && ufw allow 443/udp
ufw enable
systemctl enable --now fail2ban
dpkg-reconfigure -plow unattended-upgrades
timedatectl set-timezone America/Bogota
```

Replicar las reglas en el firewall del proveedor si existe. Docker publica puertos saltándose `ufw`: por eso solo el proxy publica puertos y `db` nunca lleva `ports:` (el `docker-compose.dev.yml`, que sí publica la base, no se copia al servidor).

### 6.5 Docker

```bash
curl -fsSL https://get.docker.com | sh
usermod -aG docker deploy
```

`/etc/docker/daemon.json`: `{"log-driver":"json-file","log-opts":{"max-size":"10m","max-file":"3"}}` y `systemctl restart docker`. Nota: pertenecer al grupo `docker` equivale a privilegios de root sobre el host; por eso la llave de despliegue se trata como credencial crítica.

### 6.6 Configuración de la aplicación en el servidor

`/opt/gdb/.env` (permisos `600`, **solo en el servidor**; los valores con `$` entre comillas simples y `$` duplicado):

```
DOMINIO=usuarios.midominio.com
POSTGRES_DB=usuarios_db
POSTGRES_USER=usuarios_admin
POSTGRES_PASSWORD=<openssl rand -base64 32>
APP_DB_USER=usuarios_app
APP_DB_PASSWORD=<openssl rand -base64 32>
AUTH_USUARIO=admin
AUTH_HASH='<hash de caddy con $ como $$>'
IMAGE_TAG=latest
GITHUB_REPOSITORY=maomauro/grupodigitalbank-crud
```

`GITHUB_REPOSITORY` va en **minúsculas** (GHCR no admite mayúsculas). Login a GHCR con un PAT de solo lectura de paquetes: `echo $PAT | docker login ghcr.io -u maomauro --password-stdin`.

### 6.7 Conectar GitHub con el VPS

- *Environments → `production`*: se puede crear en plan Free, pero **revisor requerido y restricción a la rama `main` no están disponibles** en repos privados Free (ver 2.3). Mientras tanto, el job `deploy` lleva `if: github.ref == 'refs/heads/main'` y solo `main` recibe el push de release.
- Secretos del environment:

  | Secreto | Contenido |
  |---|---|
  | `VPS_HOST` | IP o dominio del VPS |
  | `VPS_PORT` | Puerto SSH |
  | `VPS_USER` | `deploy` |
  | `VPS_SSH_KEY` | Llave privada `gdb_deploy` |
  | `VPS_KNOWN_HOSTS` | Salida de `ssh-keyscan -p <puerto> <host>` |

  Las contraseñas de la app **no** van en GitHub: viven en `/opt/gdb/.env`.
- *Packages*: tras la primera publicación, vincular el paquete al repo y mantenerlo privado.

---

## Fase 7. Entrega continua ⏳

Rama `ci/despliegue-vps`.

### 7.1 `docker-compose.prod.yml` (override)

Sustituye `build:` por imágenes versionadas. Se valida con `docker compose -f docker-compose.yml -f docker-compose.prod.yml config -q`, y esa verificación se añade al job `docker` de CI.

```yaml
services:
  negocios:
    image: ghcr.io/${GITHUB_REPOSITORY}/negocios:${IMAGE_TAG}
    build: !reset null
  web:
    image: ghcr.io/${GITHUB_REPOSITORY}/web:${IMAGE_TAG}
    build: !reset null
```

(`!reset` requiere Compose ≥ 2.24; el VPS instala la versión actual. Se verifica localmente: Compose v5.) Añadir límites de memoria y, si arrancan bien así, `read_only: true` con `tmpfs: /tmp`.

### 7.2 Workflow `.github/workflows/deploy.yml`

Disparo (D7): **el merge de `develop` a `main`**, es decir, `workflow_run` de `ci` con `conclusion == 'success'` sobre la rama `main` (checkout de `head_sha`), más `workflow_dispatch` con input `tag` para re-desplegar o hacer rollback. Los merges a `develop` ejecutan CI pero **no despliegan**. El tag `vX.Y.Z` se crea sobre `main` después del release y etiqueta la imagen.

1. **build-push:** `docker/login-action` a GHCR con `GITHUB_TOKEN` (`permissions: packages: write`), `docker/build-push-action` para `negocios` y `web`, etiquetas `sha-<corto>`, `latest` y `vX.Y.Z` en tags, caché `type=gha`. El nombre de la imagen se pasa a minúsculas. Opcional: provenance y SBOM.
2. **deploy** (environment `production`, `needs: build-push`, `concurrency: deploy-production`, sin cancelar en curso):
   - Configurar `~/.ssh` con `VPS_SSH_KEY` y `VPS_KNOWN_HOSTS` (nunca `StrictHostKeyChecking=no`).
   - Copiar `docker-compose.yml`, `docker-compose.prod.yml`, `Caddyfile`, `db/` y `deploy/*.sh` (`scp`/`rsync`).
   - Ejecutar por SSH `deploy/desplegar.sh <tag>`:

     ```bash
     set -euo pipefail
     cd /opt/gdb
     C="docker compose -f docker-compose.yml -f docker-compose.prod.yml"
     NUEVO="$1"
     PREVIO=$(grep '^IMAGE_TAG=' .env | cut -d= -f2)
     # Backup previo: el migrador puede cambiar el esquema al subir
     if $C ps --status running db | grep -q db; then ./backup.sh; fi
     sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$NUEVO/" .env
     $C pull
     if ! $C up -d --remove-orphans --wait --wait-timeout 120; then
       echo "Fallo; rollback a $PREVIO"
       sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$PREVIO/" .env
       $C up -d --wait
       exit 1
     fi
     docker image prune -f
     ```
3. **Smoke test:** `curl -fsS --retry 10 --retry-delay 5 https://$DOMINIO/health` y comprobación de la cabecera HSTS.

### 7.3 Rollback

- Automático si `up --wait` falla (también cuando el migrador termina con error).
- Manual: relanzar el workflow con un tag/SHA anterior (`workflow_dispatch` con input `tag`) o, en el VPS, cambiar `IMAGE_TAG` y `docker compose up -d`.
- Esquema: el rollback de imágenes no deshace migraciones. Se diseñan como *expand/contract* (compatibles hacia atrás); si hay que revertir datos, se restaura el backup tomado justo antes.

---

## Fase 8. Primera publicación ⏳

1. DNS resolviendo al VPS (`nslookup`).
2. VPS endurecido y `/opt/gdb/.env` completo (fase 6).
3. Secretos y environment `production` creados (6.7).
4. `develop` con las fases 3 a 5 fusionadas y CI verde; PR de release `develop` → `main` (título `release: v1.0.0`), fusionado con **merge commit**. Ese merge dispara el despliegue.
5. Ejecutar el deploy. Primer arranque: Caddy obtiene el certificado (~1 min). Si hay dudas con el DNS, probar antes con el CA de pruebas de Caddy (`acme_ca` staging).
6. En el VPS: `docker compose ps` todo `healthy`; `docker compose logs proxy | tail` sin errores ACME.
7. Verificación externa:
   - `https://<dominio>` pide usuario y clave.
   - Registrar → consultar → modificar → eliminar.
   - `http://<dominio>` redirige a HTTPS.
   - `nmap -p 1-10000 <ip>`: solo 22, 80 y 443; el 5432 no responde.
   - securityheaders.com y SSL Labs en nota A.
   - El nombre `x');alert(1);//` no ejecuta nada.
8. `reboot` del VPS y comprobar que todo vuelve solo.
9. Tag `v1.0.0` y release en GitHub.

---

## Fase 9. Operación ⏳

### 9.1 Backups de PostgreSQL

`/opt/gdb/backup.sh`, ejecutado por cron del usuario `deploy` (diario 02:30) y también por `desplegar.sh`. No se hace `source .env` (los `$$` del hash se expandirían); se usan las variables del propio contenedor:

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /opt/gdb
F="backups/usuarios_$(date +%F_%H%M).sql.gz"
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner' | gzip > "$F"
find backups -name 'usuarios_*.sql.gz' -mtime +14 -delete
```

- Cron: `30 2 * * * /opt/gdb/backup.sh >> /opt/gdb/backups/backup.log 2>&1`.
- Copia **fuera del servidor** (regla 3-2-1): `rclone` a un bucket (Backblaze B2, S3, Drive) o `rsync` a otra máquina.
- **Probar la restauración** al menos una vez en local: `gunzip -c backup.sql.gz | docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'`. Un backup sin restauración probada no cuenta.

### 9.2 Monitoreo

- Uptime externo (UptimeRobot / Better Stack) sobre `https://<dominio>/health`, con alerta a tu correo.
- Alerta de espacio en disco (`df`) por cron o agente del proveedor.
- Logs: `docker compose logs --since 1h`; rotación ya limitada en `daemon.json`.

### 9.3 Mantenimiento

| Tarea | Frecuencia |
|---|---|
| Revisar y fusionar PR de Dependabot | Semanal |
| `apt upgrade` y reinicio si cambia el kernel | Automático (`unattended-upgrades`) + revisión mensual |
| Rotar contraseñas de BD, llave de despliegue y PAT de GHCR | Cada 6 meses |
| Probar restauración de backup | Trimestral |
| Certificado | Caddy renueva solo; la alerta de uptime avisa si falla |
| Imagen base de Postgres | Menores mensual; mayores con `pg_dump` y restauración |

### 9.4 Runbook (`docs/runbook.md`)

Cómo desplegar, hacer rollback, restaurar backup, rotar secretos, y qué hacer si el certificado no se emite, el disco se llena o el VPS no responde (consola del proveedor).

---

## Fase 10. Cierre y aceptación ⏳

1. `CLAUDE.md`: actualizar "Estado conocido" y los comandos reales.
2. README: requisitos, desarrollo local, pruebas, variables, despliegue, enlaces a ADRs y runbook, insignia de CI.
3. `docs/` con arquitectura, ADRs y runbook.
4. Diagnóstico: tabla de estado, cada hallazgo → PR que lo cierra.
5. Métricas DORA básicas con el historial de Actions.

### Checklist final de aceptación

- [x] Herramientas instaladas y verificadas
- [x] Repo Git local con historial limpio y sin `obj/`, `bin/`, `.env`
- [x] `CLAUDE.md` y `docs/git-guia-tecnica.md` presentes
- [ ] Repo en GitHub, ajustes aplicados, ramas locales y remotas alineadas (`git branch -vv` sin `gone`)
- [ ] Flujo `feature → develop → main` operando; `develop` por defecto; barreras locales (hook) activas y decisión A/B/C de 2.3 documentada
- [ ] Aplicación corre en local en modo A y B; `scripts/verificar.ps1` en verde; hook activo
- [ ] H1–H8 cerrados; B1–B8 cerrados o en backlog con motivo
- [ ] Pruebas unitarias e integración pasando; cobertura ≥ 80 % en Negocios
- [ ] CI obligatorio en PR (format, build, tests, vulnerabilidades, Trivy)
- [ ] VPS: usuario sin root ni sudo, SSH por llave, firewall, fail2ban, actualizaciones automáticas
- [ ] DNS + HTTPS válido con HSTS y cabeceras de seguridad
- [ ] Aplicación protegida con autenticación
- [ ] BD solo en red interna y con rol de mínimo privilegio
- [ ] Despliegue automático con backup previo, smoke test y rollback probado (provocar un fallo a propósito)
- [ ] Backup diario, copia externa y restauración probada
- [ ] Monitoreo externo activo y alerta recibida en una prueba
- [ ] Reinicio del VPS sin intervención manual
- [ ] Documentación y runbook completos

## Riesgos y dependencias

| Riesgo | Mitigación |
|---|---|
| El proyecto nunca se ha compilado | Fase 3.2 antes de cualquier cambio de código |
| GitHub Free no protege ramas en repos privados | Verificar en 2.3; alternativas: Pro, repo público o hook + disciplina, con ADR |
| Let's Encrypt limita emisiones si el DNS está mal | Verificar DNS; probar con el CA staging de Caddy |
| Perder datos en una migración | Backup obligatorio antes de cada despliegue; migraciones *expand/contract* |
| Llave de despliegue comprometida | Llave exclusiva, `deploy` sin sudo (aunque en grupo `docker`), rotación semestral |
| Docker salta `ufw` | Solo el proxy publica puertos; el compose de desarrollo no se despliega |
| Un solo VPS (sin alta disponibilidad) | Aceptado a esta escala; backups externos + runbook permiten reconstruir |
| Dockerfile ignora `Directory.*.props` | Copiarlos antes del `restore` (3.6) |

## Decisiones pendientes (afectan a las fases 4, 6 y 7)

1. Proveedor, SO y acceso (IP, usuario) del VPS.
2. Dominio o subdominio y dónde está su DNS.
3. Si Basic Auth basta como autenticación inicial (4.2) o se quiere login propio ya.
4. Barreras de rama (2.3): se mantiene la opción A (disciplina + hook) o se pasa a B (repo público) o C (GitHub Pro). La cuenta es Free.

Resuelto: el despliegue ocurre al fusionar `develop` en `main` (D7).
