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
| VPS | **Compartido y ya en producción** (Contabo, 6 vCPU, 12 GB, 200 GB; usuario `maomauro`; IP `13.140.188.159`). Aloja PortalCV y Colfe: se audita, no se reinstala (fase 6) |
| Dominio y DNS | `sitiosapps.com` en Cloudflare (proxy activo); cada proyecto recibe `<proyecto>.sitiosapps.com`. Este: `gdb-crud.sitiosapps.com` |
| TLS y entrada | Cloudflare → `portalcv-nginx-prod` (único que publica 80/443, certificado de origen en `/etc/nginx/ssl/`) → Caddy interno por la red compartida con el alias `gdb-crud-web`. Detalle en [ADR 0011](adr/0011-despliegue-detras-del-nginx-compartido.md) (aceptada) |
| Carpetas en el VPS | `/srv/sitiosapps/gdb-crud/` y `/srv/sitiosapps/_backups/gdb-crud/` |
| Registro de imágenes | GitHub Container Registry (`ghcr.io`) |
| Despliegue | GitHub Actions → SSH con llave restringida a un script de entrada → `docker compose pull && up -d` |
| Migraciones | Scripts SQL numerados + contenedor one-shot con `psql` |
| Autenticación mínima | Basic Auth en Caddy (un usuario y una clave compartidos); login propio es mejora futura |

## Resumen de fases

| # | Fase | Resultado | Estado | Esfuerzo |
|---|---|---|---|---|
| 0 | Herramientas locales | Máquina lista | ✅ | — |
| 1 | Repositorio local | Git inicializado, convenciones, commit base | ✅ | — |
| 2 | Repositorio remoto | GitHub con `main` y `develop`, ajustes de merge; barreras según 2.3 | ✅ | 1 h |
| 3 | Ambiente local y primer build | Compose de desarrollo, build real, scripts de verificación | ✅ | 3–4 h |
| 4 | Remediación del diagnóstico | Hallazgos corregidos con pruebas | 🔄 | 2–3 días |
| 5 | Integración continua | Build + tests + escaneo en cada PR | ✅ | 3–4 h |
| 6 | Preparar el VPS compartido | Auditoría, Cloudflare, vhost en el nginx compartido, llave de despliegue restringida | ⏳ | 3–4 h |
| 7 | Entrega continua | Imágenes en GHCR y despliegue automático con rollback | ✅ | 4–6 h |
| 8 | Primera publicación | Sistema en `https://gdb-crud.sitiosapps.com` | ⏳ | 1–2 h |
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
| P1 | Auditoría del VPS compartido, DNS y certificado en Cloudflare, vhost en el nginx compartido, conexión con GitHub | 6 |
| P1 | CD con backup previo, smoke test y rollback | 7 |
| P1 | Primera publicación | 8 |
| P1 | Backup diario y restauración probada | 9.1 |
| P2 | Monitoreo externo, runbook, rotación de secretos | 9 |
| P2 | H8 arquitectura y ADRs, B6 zona horaria, B8 WSDL solo en desarrollo | 4.8 |
| P2 | Cobertura ≥ 80 %, Trivy, CodeQL, hadolint | 5 |
| P3 | login propio (Identity/OIDC), métricas DORA | 4.8, 10 |

Camino crítico: 2 → 3 → H1 → H5 → H4 → H2 → H6 → pruebas mínimas → CI → 6 → 7 → 8 → backup.

## Documentos de soporte

| Documento | Para qué | Estado |
|---|---|---|
| [CLAUDE.md](../CLAUDE.md) | Contexto del proyecto para Claude Code | ✅ se actualiza al cerrar cada fase |
| [ciclo-de-vida-desarrollo.md](ciclo-de-vida-desarrollo.md) | Ciclo local → GitHub, brechas, DoR/DoD, decisiones | ✅ |
| [git-guia-tecnica.md](git-guia-tecnica.md) | Ramas locales y remotas, flujo, protecciones | ✅ |
| [diagnostico-2026-10-09.md](diagnostico/diagnostico-2026-10-09.md) | Hallazgos de partida | ✅ |
| [arquitectura.md](arquitectura.md), [adr/](adr/README.md) | Vista de conjunto (C4) y registro de decisiones | ✅ (H8) |
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

## Fase 2. Repositorio remoto (GitHub) ✅

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
- ✅ `.github/dependabot.yml` (issue #30): `nuget` (con grupos `corewcf` y `pruebas`), `docker` (una entrada por carpeta con Dockerfile), `docker-compose` y `npm` (herramienta de navegador); semanal los lunes, `target-branch: develop`, prefijo `build(deps)` y sin saltos de versión mayor de .NET ni de PostgreSQL.
  - **`github-actions` se quitó a propósito (2026-10-10):** Dependabot fallaba con `/action.yml or /.github/workflows/<anything>.yml not found` porque el repositorio aún no tiene workflows. **Reañadido en la fase 5**, junto con `ci.yml` (`package-ecosystem: "github-actions"`, `directory: "/"`, prefijo `ci`).
- ✅ Seguridad (activado el 2026-10-09 por API): Dependabot alerts y actualizaciones automáticas de seguridad (0 alertas abiertas). Secret scanning y push protection no están disponibles en repos privados con plan Free.
- *Actions → General*: `GITHUB_TOKEN` en solo lectura por defecto.

### 2.3 Protección de `main` y `develop` (cuenta Free)

> **Verificado el 2026-10-09:** la API respondió `403 Upgrade to GitHub Pro or make this repository public`. La cuenta Free no admite protección de ramas ni *rulesets* en repos privados.
>
> **Corrección del 2026-10-11:** este párrafo decía también «ni reglas de environments», y **eso era falso**: al configurar el *environment* `production` en la interfaz, la restricción *Deployment branches and tags* (solo `main`) **sí está disponible y quedó aplicada**. Lo que no se ha comprobado es la opción de *revisores requeridos*.

Vigente (**opción A**): las reglas se sostienen con el hook `pre-push` (rechaza push directo a `main` y `develop`, fase 3.5), el CI visible en cada PR, el checklist del PR y los ajustes de merge. Es una barrera local: se salta con `--no-verify` o desde otro equipo. Detalle en la guía de Git, §10.

Si se quiere imponerlas en el servidor:

| Opción | Efecto | Costo |
|---|---|---|
| A. Disciplina + hook `pre-push` | Sin imposición en el servidor | Gratis; **vigente** |
| B. Repo público | Protección y environments disponibles; el código queda visible | Gratis; exige cero secretos en el historial y revisar `appsettings` |
| C. GitHub Pro | Protección completa en privado | Pago mensual |

Con B o C: PR obligatorio en `main` y `develop`, checks `ci`, `migrador` y `docker` obligatorios (existen desde la fase 5), sin force-push ni borrado, historial lineal en `develop`, aplicar a administradores y proteger tags `v*`. Se registra la elección en `docs/adr/0005-proteccion-de-rama.md`.

### 2.4 Issues y primer flujo

- Crear un issue por hallazgo H1–H8 y por brecha pendiente del ciclo de vida (L6–L13), con su prioridad.
- Subir este ajuste de documentación como primer PR: rama `docs/ajuste-plan-trabajo` → `develop`, fusionado con squash.
- Primer release de documentación opcional: `develop` → `main` solo cuando haya algo desplegable (los cambios de documentación no requieren liberar).

### 2.5 Criterio de salida

Cumplido el 2026-10-09: `main` y `develop` publicadas, `develop` por defecto, 8 PR fusionados (#1, #25 a #29, #31 y #32) con el flujo feature → develop y las ramas de trabajo borradas (`git branch -vv` sin huérfanas). Quedó una excepción de forma: varios PR se fusionaron con merge commit en vez de squash, lo que alarga el historial pero no lo daña. Pendiente de confirmar: que Dependabot abra su primer PR de versiones (corre los lunes o al cambiar la configuración).

---

## Fase 3. Ambiente local y primer build ✅

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

Hecho (issue #6), en `.vscode/`:

- `launch.json`: depurar **Negocios (:5081)** y **Presentacion (:5080)** con F5, y la configuración compuesta **«Modo B: Negocios + Presentacion»**, que antes levanta la base de datos. Usan rutas absolutas al `.dll`, `cwd` del proyecto y `ASPNETCORE_ENVIRONMENT=Development` (necesario para que se carguen los `user-secrets`).
- `tasks.json`: `build` (por defecto), `test: unitarias (sin Docker)` (por defecto de pruebas), `test: todas`, `verificar: rapida` y `completa`, `verificar: herramientas`, `watch: negocios` y `watch: presentacion`, `bd: iniciar (modo B)`, `stack: iniciar (modo A)`, `stack: detener (conserva datos)` y `stack: logs`.
- `extensions.json`: C# Dev Kit, C#, Docker, EditorConfig, YAML, GitHub Actions, GitHub Pull Requests y SQLTools con su driver PostgreSQL.
- `settings.json`: solución por defecto, fin de línea LF, formato al guardar y una conexión SQLTools a la base local (`askForPassword`, sin contraseña versionada).

Probado fuera de VS Code: JSON válidos, las tareas ejecutan sus comandos, y el `.dll` lanzado como lo hace el depurador (ruta absoluta, `cwd`, variables) sirve el flujo completo. No se pudo ejercitar el depurador de VS Code en sí (puntos de interrupción).

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

- ✅ `tests/` con los tres proyectos de 4.7, añadidos a `GrupoDigitalBank.slnx` y con una prueba real cada uno (issue #7): `Negocios.Tests` (validador), `Presentacion.Tests` (controlador con cliente SOAP simulado) y `Negocios.IntegrationTests` (repositorio contra PostgreSQL 17 real en Testcontainers, con los scripts de `db/` aplicados en orden). Versiones de xUnit y compañía = las de la plantilla del SDK (xunit 2.9.3, runner 3.1.4, Test.Sdk 17.14.1, coverlet 6.0.4).
- ✅ `Directory.Packages.props` con `ManagePackageVersionsCentrally=true` y versiones exactas (CoreWCF 1.9.1, Npgsql 10.0.3, System.ServiceModel.Http 8.1.2); los `.csproj` ya no llevan `Version` (issue #9).
- ✅ Los Dockerfile copian `global.json`, `Directory.Build.props` y `Directory.Packages.props` antes del `restore`; verificado que las imágenes contienen esas versiones exactas (issue #9).

### 3.7 Criterio de salida

Cumplido el 2026-10-09: `scripts/verificar.ps1` en verde, el stack arranca en modo A y funciona de punta a punta, modo B con el mismo resultado, hook activo y VS Code configurado. Los PR #25 a #28 y este llevaron todo a `develop`; falta únicamente comprobar F5 con puntos de interrupción en tu VS Code. Uno o varios PR pequeños llevan esto a `develop` (la fase 3 no se libera a `main` hasta que haya algo desplegable).

---

## Fase 4. Remediación del diagnóstico ⏳

Una rama y un PR por ítem, cada uno con su issue. Ramas en la tabla de [git-guia-tecnica.md](git-guia-tecnica.md). Orden: 4.1 → 4.4 → 4.5 → 4.2 → 4.6 → 4.7 → 4.8.

### 4.1 H1 · XSS en el botón Eliminar (alta, P0) ✅

Hecho (issue #2):

- `Views/Usuarios/Consultar.cshtml`: se quitó `onsubmit="return confirm('…@u.Nombre…')"`. Ahora el formulario lleva `data-confirmar="¿Eliminar a @u.Nombre?"` (Razor lo codifica como atributo) y un único listener en `wwwroot/js/site.js`, cargado desde el layout con `<script src defer>`, que lee el atributo **como texto** y se lo pasa a `confirm()`. Nada del nombre llega a un contexto ejecutable.
- No quedan manejadores en línea en ninguna vista (`grep` de `on*=`, `<script` y `javascript:` en `Views/`).
- Pruebas de regresión (`UsuariosVistasTests`, con `WebApplicationFactory<Program>` y cliente SOAP simulado): cinco cargas maliciosas (`x');alert(1);//`, `");alert(1);//`, `<script>`, `"><img onerror>`, `'-alert(1)-'`), el nombre se muestra como texto codificado, el formulario usa `data-confirmar` y el layout sirve `site.js`. **Se verificó en rojo con el código anterior** (las 5 cargas fallaban) y en verde con la corrección.
- Comprobación del navegador con jsdom (fuera del repo): `confirm` recibe el nombre literal, cancelar detiene el envío, aceptar lo permite, otros formularios no se afectan y la carga nunca se ejecuta.
- Soporte: `public partial class Program;` en `Presentacion/Program.cs` y `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 (cota inferior segura para cualquier runtime ≥ 10.0.0).
- Pendiente (defensa en profundidad, B4): CSP en Caddy sin `'unsafe-inline'` para scripts; ya es posible porque no hay scripts en línea. Los estilos en línea tampoco existen en las vistas.
- ✅ Comprobación con **clic real en Chrome y Edge** (`scripts/probar-navegador.ps1`, con `puppeteer-core`): un usuario llamado `x');alert(1);//` se muestra como texto, el clic en Eliminar abre **un solo** diálogo y es un `confirm` con el nombre literal (ningún `alert`), cancelar no elimina nada y aceptar elimina solo ese usuario de prueba. Cierra lo que había quedado pendiente en el PR de H1.

### 4.2 H2 · Autenticación mínima (alta, P1) ✅

**Qué es:** Basic Auth en Caddy. El navegador pide usuario y clave y el proxy los verifica antes de dejar pasar cualquier petición a la aplicación; no hay código de autenticación en .NET. Es **un solo usuario y una sola clave compartidos**: cierra el acceso anónimo desde internet, pero no distingue personas ni tiene «cerrar sesión». Un login propio (ASP.NET Core Identity), un proveedor externo (OIDC) o restringir por IP quedan como mejora futura, a registrar en el ADR `0004-autenticacion`.

Hecho (issue #12):

- `Caddyfile`: `/health` queda abierto (coincidencia exacta); **todo lo demás** —páginas, estáticos y cualquier método HTTP— pasa por `basic_auth` con `AUTH_USUARIO` y `AUTH_HASH`, y el proxy elimina el encabezado `Authorization` antes de reenviar a la aplicación (`header_up -Authorization`).
- `docker-compose.yml`: el servicio `proxy` recibe `AUTH_USUARIO` y `AUTH_HASH`, ambas obligatorias (`${VAR:?}`). **Seguro por defecto:** el `.env.example` trae un valor de ejemplo que *no* es un hash válido a propósito, y con él Caddy se niega a cargar (`illegal base64 data`), así que el proxy no arranca sin protección.
- `web` no tenía `/health` (solo Negocios; el diagnóstico se equivocó en ese punto). Se creó `GET /health` en `Presentacion/Program.cs` (texto plano `ok`, sin consultar el servicio de negocios), con `SaludTests`, escritas primero y vistas en rojo. Sirve también para la prueba de humo de la fase 7 y para B3.
- `scripts/generar-hash-auth.ps1 [-Usuario admin] [-ActualizarEnv]`: pide la clave sin mostrarla (mínimo 12 caracteres), genera el hash bcrypt con Caddy (coste 14) y escribe `AUTH_USUARIO` y `AUTH_HASH` en el `.env` con el entrecomillado correcto. La clave en claro no se guarda en ningún archivo.
- `scripts/probar-acceso.ps1`: batería de **38 comprobaciones** contra un stack con el proxy en marcha (ver abajo).

**Trampas encontradas (por si se vuelve a tocar):**

- `docker compose --env-file`/`.env`: el hash va **entre comillas simples** y *sin* duplicar los `$`. Dentro de comillas simples Compose no interpola, y el hash llega intacto al contenedor (verificado, 60 caracteres). El plan original decía duplicar `$$`; no hace falta con comillas simples.
- PowerShell 5.1 **no sirve para enviar la clave a Caddy por la tubería**: antepone un BOM UTF-8 y añade CRLF, de modo que el hash salía de otra cadena y la clave correcta daba 401. Tampoco por argumento (visible en la lista de procesos) ni con comillas dobles (PowerShell 5.1 las altera al llamar a `docker`). La solución del script: la clave viaja en **base64 por una variable de entorno** y la tubería `base64 -d | caddy hash-password` corre dentro del contenedor. Probado con claves con espacios, comillas, `$`, `&|<>!` y acentos.
- `caddy hash-password` por entrada estándar descarta **el último byte** leído (asume `\n`): sin salto de línea final falla con `EOF`.
- `Results.Ok("ok")` en minimal API devuelve `"ok"` con comillas (JSON); para el chequeo de salud se usa `Results.Text("ok")`.

**Verificación (`scripts/probar-acceso.ps1`, HTTPS local con `DOMINIO=localhost`, perfil `con-proxy`):**

- Credenciales: sin ellas, con clave incorrecta y con usuario incorrecto → 401; correctas → 200.
- Todo lo demás exige credenciales: `GET` de páginas y estáticos (`/css`, `/js`) y `POST/PUT/DELETE/PATCH/OPTIONS` → 401.
- `/health` abierto **solo** él: `/HEALTH` y `/%68ealth` (equivalentes) → 200; `/health/`, `/health/x`, `/healthz`, `/health.json`, `/healthcheck`, `/health;x`, `/health/../Usuarios/Consultar` y `//Usuarios/Consultar` → 401. `//health` → 404 (Caddy lo trata como `/health` y la aplicación no tiene esa ruta; no expone nada protegido). Su cuerpo es solo `ok`.
- HTTP redirige a HTTPS (308) antes de pedir credenciales; el 401 trae `WWW-Authenticate: Basic`.
- **Aplicación completa a través del proxy** (antiforgery, cookies y cabeceras reenviadas): registrar, aparecer en la grilla, modificar, validación y eliminar, dejando intactos los usuarios previos.

**Límites conocidos:**

- **Sin bloqueo por intentos fallidos.** El bcrypt de coste 14 hace que cada intento tarde ≈0,9 s (5 intentos: 4,5 s), pero Caddy no bloquea la IP. Mitigaciones previstas: clave larga (≥ 12, idealmente 20+), `fail2ban` sobre el log de Caddy en el VPS (fase 6) o un plugin de rate limiting.
- Un solo usuario compartido; rotar la clave es volver a ejecutar `generar-hash-auth.ps1 -ActualizarEnv` y recrear el proxy.
- En modo A de desarrollo sin `--profile con-proxy` la web se sirve en `http://localhost:8080` **sin autenticación** (solo accesible desde este equipo, `127.0.0.1`). La autenticación se prueba con `--profile con-proxy`, que exige `AUTH_USUARIO` y `AUTH_HASH` en el `.env`.
- Los `.env` locales existentes deben añadir `AUTH_USUARIO` y `AUTH_HASH` (o Compose falla con un mensaje claro).

### 4.3 H3 · Git ✅

Cerrado en la fase 1.

### 4.4 H5 · Migraciones (media, P1) ✅ — va antes que H4

Hecho (issue #10):

- `db/migrate.sh` (POSIX sh, pasa `shellcheck`, LF y bit de ejecución): crea `schema_migrations(version, checksum, aplicada_en)`, recorre `db/NNN_*.sql` en orden y aplica solo los que faltan. Cada script corre con `psql -v ON_ERROR_STOP=1 --single-transaction` **junto con su registro**, así que un fallo no deja nada a medias. Guarda el **checksum sha256** y **aborta si un script ya aplicado cambió** (los aplicados no se editan). Pasa `app_pass` a los scripts desde `APP_DB_PASSWORD` (lo usará H4).
- Servicio `migrador` en `docker-compose.yml` (`postgres:17-alpine`, `./db` montada en `/migraciones`, conecta como `POSTGRES_USER`, `restart: "no"`, espera a que `db` esté sana). `negocios` depende de `migrador: service_completed_successfully`. Se quitó el montaje en `docker-entrypoint-initdb.d`.
- **No hizo falta recrear el volumen**: el `001` es idempotente (`CREATE TABLE IF NOT EXISTS`, `CREATE OR REPLACE FUNCTION`), así que el migrador lo registró sobre la base de desarrollo existente sin tocar los datos. Para scripts futuros, mantener la idempotencia cuando sea razonable, pero ya no es obligatoria: el migrador garantiza que cada uno corre una sola vez.
- Pruebas (`scripts/probar-migrador.ps1`, base PostgreSQL desechable, 16 comprobaciones): base vacía, segunda ejecución sin repetir, script nuevo válido, script con error (transacción revertida y sin registro), script aplicado modificado (aborta) y base existente sin registro (conserva los datos).
- Verificado en el stack real: caso A (base existente con datos, sobre la que se aplicó y registró el `001`), caso B (base nueva en un proyecto Compose aislado, Negocios arranca) y caso C (una migración rota deja a `negocios` en `Created`, sin arrancar).
- Regla vigente: **los scripts aplicados nunca se editan**; todo cambio es un `00N_…sql` nuevo. Si dos ramas crean el mismo número, la segunda en fusionarse renumera.
- Hecho en la fase 5: el trabajo `migrador` del CI ejecuta `shellcheck db/migrate.sh` y `scripts/probar-migrador.ps1` (con `pwsh` en Linux, tras hacer portables sus rutas).
- H4 (hecho): el migrador recibe `APP_DB_USER`/`APP_DB_PASSWORD` y sincroniza la contraseña del rol en cada ejecución.

### 4.5 H4 · Rol de aplicación en PostgreSQL (media, P1) ✅ — depende de 4.4

Hecho (issue #11). Modelo de roles:

| Rol | Variable | Qué es |
|---|---|---|
| `usuarios_admin` | `POSTGRES_USER` / `POSTGRES_PASSWORD` | Propietario de la base y quien ejecuta las migraciones (superusuario del contenedor). **La aplicación no lo usa.** |
| `usuarios_app` | `APP_DB_USER` / `APP_DB_PASSWORD` | Rol de la aplicación: `LOGIN`, sin superusuario, sin crear bases ni roles. Solo puede **ejecutar** `fn_usuario_crud`. |

- `db/002_rol_aplicacion.sql` (aplicado por el migrador): crea o actualiza el rol con `\gexec` + `format(%I, %L)` (nombre y contraseña correctamente entrecomillados, contraseña inyectada por `-v app_pass`, nunca versionada); retira los permisos de `PUBLIC` sobre la base, el esquema, la tabla y la función; declara `fn_usuario_crud` como `SECURITY DEFINER SET search_path = public, pg_temp` (con `ALTER FUNCTION`, sin editar el `001`); concede solo `CONNECT`, `USAGE` sobre `public` y `EXECUTE` sobre la función. No se concede nada sobre la tabla.
- `db/migrate.sh` exige `APP_DB_USER` y `APP_DB_PASSWORD` (falla en voz alta si faltan), rechaza que sean el mismo usuario que migra y **sincroniza la contraseña del rol en cada ejecución**. Así rotarla es cambiar `APP_DB_PASSWORD` y redesplegar, aunque el `002` ya esté aplicado.
- `docker-compose.yml`: el servicio `migrador` recibe las dos variables y `negocios` se conecta con `APP_DB_USER`/`APP_DB_PASSWORD`. Ambas son obligatorias (`${VAR:?mensaje}`): con un `.env` viejo, Compose falla con un mensaje claro en lugar de seguir con valores vacíos. `.env.example` documenta los dos roles.
- Pruebas de integración (`RolAplicacionTests`, como el rol de aplicación contra PostgreSQL real): no puede `DROP TABLE` (`must be owner`), ni leer/insertar/borrar la tabla, ni crear objetos ni borrar la función (SQLSTATE 42501); no es superusuario ni puede crear bases/roles; solo ese rol tiene `EXECUTE` (otro rol y `PUBLIC` no); la función es `SECURITY DEFINER` con `search_path` fijo. Se comprobó con una **prueba de mutación** (dar permisos de más en el script): 3 de estas pruebas la detectan. `PostgresFixture` ahora aplica los scripts con `psql` dentro del contenedor, igual que el migrador, y expone conexiones de aplicación y de administrador.
- `scripts/probar-migrador.ps1` ampliado a 8 escenarios (31 comprobaciones): además de lo de H5, el rol sin superusuario, mínimo privilegio, rotación de contraseña (la nueva funciona y la anterior deja de funcionar) y validación de entrada.
- Verificado en el stack real, en modo A y modo B, con la aplicación conectada como rol limitado: flujo completo OK, y directamente contra la base el rol recibe `must be owner of table usuario` (DROP), `permission denied for table usuario` (SELECT, DELETE, TRUNCATE) y `permission denied for schema public` (CREATE).
- **Base de desarrollo migrada**: en un volumen existente `POSTGRES_USER` era superusuario y llamarse igual que el rol de la app, así que no se puede convertir en caliente. Se recreó el volumen con respaldo previo verificado (volcado lógico + copia física del volumen, comprobada levantando un Postgres sobre ella) y se restauraron los datos y la secuencia. Los `.env` locales existentes deben añadir `APP_DB_USER`, `APP_DB_PASSWORD` y cambiar `POSTGRES_USER` (idealmente a `usuarios_admin`) **antes** de recrear el volumen; el volumen viejo se identifica por el nombre `*_pgdata`.
- Riesgo residual documentado: la función pertenece al administrador (superusuario) y corre con sus privilegios (`SECURITY DEFINER`). Está acotada porque el SQL es estático y parametrizado y el `search_path` está fijo; un rol propietario sin superusuario (`NOLOGIN`) sería más estricto pero obliga a reasignar el dueño en cada migración futura. Se deja como mejora.
- En producción (fase 6.6) el `.env` del servidor ya prevé `POSTGRES_USER=usuarios_admin` y `APP_DB_*`.

### 4.6 H6 · Claves de Data Protection (media, P1) ✅

**Problema:** ASP.NET Core firma el token antiforgery y las cookies de TempData con claves de Data Protection. En un contenedor las claves viven en memoria; al recrearlo (cada redespliegue) se generan otras, y un formulario abierto antes falla con **400**. Reproducido antes de corregir: el formulario se aceptaba (302) sin redesplegar y daba 400 después de `--force-recreate`.

Hecho (issue #13):

- `Presentacion/Program.cs`: `AddDataProtection().SetApplicationName("GrupoDigitalBank")` y, si existe la configuración `DataProtection:RutaClaves`, `PersistKeysToFileSystem`. En Docker la ruta es un volumen (`DataProtection__RutaClaves=/keys`); en desarrollo local se omite y rige el valor por omisión de ASP.NET Core (el perfil del usuario). El nombre de aplicación es fijo para que las claves no dependan de la ruta del contenido.
- `Presentacion/Dockerfile`: `RUN mkdir /keys && chown $APP_UID /keys` antes de `USER $APP_UID`. El directorio debe existir en la imagen con el dueño no root: un volumen nombrado nuevo hereda ese dueño y el proceso (UID 1654) puede escribir.
- `docker-compose.yml`: volumen nombrado `dpkeys` montado en `/keys` del servicio `web`, y la variable de entorno. Solo `web` lo monta.
- Pruebas (`ClavesProteccionTests`, dos `WebApplicationFactory` como «dos despliegues»): las claves se escriben en el directorio configurado (que la aplicación crea si no existe); un formulario abierto en el primer despliegue es válido en el segundo (302); y un **control** con directorios distintos lo rechaza (400), para demostrar que las pruebas discriminan. Se escribieron primero: persistencia y control en rojo con el código anterior. La prueba «tras redespliegue» pasa en Windows aun sin la corrección, porque allí las claves se guardan por omisión en el perfil de usuario; en un contenedor Linux sin perfil sí falla, que es lo que se comprobó en Docker.
- Verificado en Docker: tras `--force-recreate`, tras `--build --force-recreate` y tras `down` + `up` (sin borrar volúmenes) el formulario abierto antes se acepta (302). Control: al borrar el volumen `dpkeys` el formulario viejo se invalida (400), como corresponde. La clave aparece como `key-*.xml` (modo 600, dueño `app`) en el volumen.

**Riesgo residual (aviso estándar de ASP.NET Core `No XML encryptor configured`):** las claves quedan **sin cifrar** en el volumen. Solo firman antiforgery y cookies de TempData; el archivo es privado a `web`; y para usar una clave robada habría que pasar primero el Basic Auth. Cifrarlas (`ProtectKeysWithCertificate`) exigiría gestionar un certificado y no compensa a esta escala. Si en el futuro hubiera sesiones o cuentas de usuario, reevaluarlo.

**Operación:** el volumen `dpkeys` no es crítico para los backups (perderlo solo invalida los formularios abiertos y las cookies vigentes en ese momento), pero conviene **no** borrarlo en un redespliegue. `docker compose down -v` lo elimina junto con los demás.

### 4.7 H7 · Pruebas (media, P1) ✅

Hecho (issue #14): **141 pruebas** (51 unitarias de Negocios, 62 de Presentación y 28 de integración con PostgreSQL real) y cobertura medida con un mínimo exigido.

| Proyecto | Pruebas | Qué cubre |
|---|---|---|
| `GrupoDigitalBank.Negocios.Tests` | 51 | `UsuarioValidador` en cada límite (nombre 0/1/100/101 y con espacios, fecha 1899-12-31/1900-01-01/hoy/mañana/`default`, sexo `M`/`F`/minúscula/vacío/nulo, acumulación y orden de errores) y `UsuarioService` con el repositorio simulado: cada operación, cada código de fault (`VALIDACION`, `NO_ENCONTRADO`, `ERROR_DATOS`, `ERROR_INTERNO`), que los errores técnicos **se registran en el log y no se filtran al cliente**, y que un «no encontrado» no se registra como error |
| `GrupoDigitalBank.Negocios.IntegrationTests` | 28 | Repositorio y `fn_usuario_crud` contra PostgreSQL real, conectados como el rol de aplicación: CRUD completo, recorte y normalización, orden por id, inexistentes (`null`/`false`/lista vacía), aislamiento entre usuarios, y la base como última red (sexo inválido, fecha < 1900 o futura con su restricción, nombre de 101 caracteres, operación inválida `22023`); más los permisos del rol (H4) |
| `GrupoDigitalBank.Presentacion.Tests` | 62 | Controlador (cada acción, camino feliz y errores del servicio mostrados de forma amigable), `UsuarioViewModel` (DataAnnotations y conversión), `UsuarioClient` ante un servicio caído, vistas renderizadas (regresión H1, `Editar`, página de error) y `/health` y claves de Data Protection (H6) |

Cobertura (`scripts/cobertura.ps1`, coverlet + ReportGenerator, une los tres proyectos):

| Ensamblado | Antes | Ahora |
|---|---|---|
| `GrupoDigitalBank.Negocios` | 41,6 % (29,8 % contando código generado) | **100 %** (89/89 líneas) — mínimo exigido 80 % |
| `GrupoDigitalBank.Presentacion` | 35,3 % | 94 % (con las pruebas de contrato de B2, #19; antes 87 %: faltaba el camino feliz de `UsuarioClient`) |

`coverlet.runsettings` excluye `Program.cs` (arranque), los `*.g.cs` generados por fuentes (invocador de operaciones de CoreWCF y `LoggerMessage`) y el código de las propias pruebas. El script devuelve código 1 si Negocios baja del umbral (`-Umbral`, por omisión 80; `-SinUmbral` solo informa). `ReportGenerator` va fijado en `.config/dotnet-tools.json` (5.5.11). El CI de la fase 5 debe ejecutar este script.

**Las pruebas detectan errores reales (pruebas de mutación manuales):** se aplicaron 21 mutaciones al código de producción, una por una —`>` por `>=` en cada límite, filtrar el detalle interno de un error de BD al cliente, quitar un `Trim` (validador, repositorio y modelo), invertir una condición, dejar de registrar un error, no persistir las claves de Data Protection, volver a un `onsubmit` en línea (H1)— y **las 21 fueron detectadas**. El código quedó intacto (`git status` limpio). El 100 % de líneas por sí solo no demostraría nada; esto sí.

Notas de implementación:

- Los tres proyectos llevan `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Sin él, `CoreWCF.Primitives` arrastra `System.Security.Cryptography.Xml` 8.0.x con vulnerabilidad alta (NU1903) que en la aplicación queda podada por el framework y en un proyecto de prueba plano no.
- `PostgresFixture` aplica los scripts `db/NNN_*.sql` en orden con `psql` dentro del contenedor (igual que el migrador, con `app_user` y `app_pass`) y expone dos conexiones: `DataSource` (rol de aplicación, el que usa Negocios) y `AdminDataSource` (propietario, solo para preparar y verificar permisos). Las pruebas de la clase comparten la base: cada una usa nombres únicos y no depende del orden ni del conteo.
- Las pruebas de integración llevan `[Trait("Categoria", "Integracion")]`; `dotnet test --filter "Categoria!=Integracion"` las omite si no hay Docker.
- `UsuarioClientTests` usa un servidor TCP local que **acepta y cierra la conexión al instante** para simular un servicio caído: contra un puerto cerrado cada llamada tardaba ≈2 s en Windows (14 s en total) y ahora toda la clase tarda milisegundos, sin depender de que un puerto concreto esté libre.
- Los límites de fecha usan `DateTime.Today`: siguen sujetos a la zona horaria del equipo (hallazgo B6, #17). Las pruebas de base usan +2 días para la fecha futura y evitan el borde.
- **Hallazgo nuevo (issue #41):** la base acepta un nombre vacío o de solo espacios; solo existen `CHECK` de sexo y fecha. Se comprobó con una transacción revertida. No se congeló en una prueba como comportamiento deseado. Resuelto con `db/004` (ver la tabla de hallazgos).

Resuelto después: el camino feliz y el fault reales del cliente SOAP y la comparación de contratos (B2, #19) y `TimeProvider` (B6, #17).

### 4.8 H8 y baja prioridad

| Ítem | Estado |
|---|---|
| H8 `docs/` vacío | ✅ `docs/arquitectura.md` (C4 contexto, contenedores y componentes en Mermaid, flujos, seguridad, calidad y despliegue) y `docs/adr/` con 10 ADR (0001 CoreWCF, 0002 sin ORM, 0003 contrato duplicado, 0004 Basic Auth, 0005 flujo de ramas y GitHub Free, 0006 zona horaria, 0007 migraciones, 0008 rol de mínimo privilegio, 0009 cabeceras y CSP, 0010 verificación). Los 6 diagramas se validan con Mermaid 11 en Chrome (analizar y dibujar) y los 72 enlaces relativos apuntan a archivos existentes, con `scripts/verificar-docs.ps1 [-Diagramas]` (con control negativo: falla ante un enlace o un diagrama rotos); el CI de la fase 5 puede ejecutarlo. La vista de despliegue en producción queda **provisional** hasta la fase 6 (infraestructura real del VPS) |
| B1 versiones flotantes | ✅ Hecho en 3.6 |
| B2 contrato duplicado | ✅ (#19) ADR 0003 y `tests/GrupoDigitalBank.Contrato.Tests` (11 pruebas): el host real de Negocios en Kestrel con un repositorio en memoria, el `UsuarioClient` real de Presentación (camino feliz, validación y no encontrado) y una comparación por reflexión del contrato (espacio de nombres, operaciones, parámetros, faults, orden y tipo de cada campo). Sustituye a comparar el WSDL: éste solo se publica en desarrollo (B8) y la reflexión muestra la diferencia exacta. 7 mutaciones detectadas y 3 más que ni compilan |
| B3 healthchecks | ✅ Ver abajo |
| B4 cabeceras y CSP | ✅ Ver abajo |
| B5 contraseña en `appsettings.json` | ✅ Ver abajo |
| B6 zona horaria | ✅ Ver abajo |
| B7 paginación | ✅ (#18) [ADR 0012](adr/0012-paginacion-en-el-servidor.md). `db/005_paginacion_usuarios.sql` (`fn_usuario_pagina`: página + total en una sentencia, `SECURITY DEFINER`, solo el rol de aplicación); operación SOAP nueva `ConsultarPagina` (la anterior no cambia: *expand/contract*); Negocios valida (página 1–100 000, tamaño 1–100) y devuelve la última página si la pedida no existe; la grilla usa `?pagina=&tamano=` (10/25/50) con Anterior, Siguiente y «Página X de Y · N usuarios», y Eliminar vuelve a la misma página. **Verificado de punta a punta en Docker** con la base nueva (25 usuarios: páginas 1–3, fuera de rango → última, valores inválidos → por omisión; al borrar el último usuario de la página 3 se cae en «Página 2 de 2»). 302 pruebas; Presentación 95 %. 25 mutaciones (servicio, SQL, modelo, controlador, vista y contrato) detectadas. Dos trampas: la ruta por omisión hace que los enlaces salgan como `/?pagina=…`, y una prueba de integración que renombraba un usuario contaminaba a las demás (comparten base) |
| B8 WSDL solo en desarrollo | ✅ Ver abajo |
| Nuevo (#41) | ✅ `db/004_nombre_no_vacio.sql`: `CHECK (nombre ~ '\S')` (al menos un carácter que no sea blanco; más amplio que `btrim`, que no recorta tabuladores). Antes de crear la restricción comprueba si hay filas inválidas y, si las hay, aborta con un mensaje que da cuántas y sus ids, sin modificar nada. Es idempotente (el escenario 6 del migrador lo exigió: una base sin registro que ya tiene la restricción). 8 pruebas de integración nuevas llaman a la función sin pasar por el repositorio (que recorta el nombre) y `probar-migrador.ps1` suma el escenario 9. Mutación a `btrim(nombre) <> ''` detectada |

**Zona horaria hecha (issue #17, B6):**

- **Problema:** la fecha de nacimiento no puede ser futura, pero «hoy» salía de `DateTime.Today` del servidor (UTC en el contenedor) y de `CURRENT_DATE` en la base (la zona de la *sesión*, también UTC). Colombia es UTC-5, así que **entre las 19:00 y la medianoche** la fecha UTC ya es la de mañana y se aceptaba una fecha futura. Reproducido en Docker a las 19:32 (hora de Bogotá).
- **Regla de negocio:** «hoy» es la fecha de `America/Bogota` (UTC-5 todo el año, sin horario de verano).
- **Negocios:** `UsuarioValidador.Validar(usuario, hoy)` recibe «hoy» como parámetro (puro y determinista). `UsuarioService` lo calcula con un `TimeProvider` cuya zona local es la de negocio (`ProveedorDeTiempoConZona`, en `Negocios/Tiempo/ZonaHorariaNegocio.cs`). La zona se configura con `Negocios:ZonaHoraria` (variable `Negocios__ZonaHoraria`; por omisión `America/Bogota`) y **el host no arranca** si el id no es válido, con un mensaje que dice qué clave corregir. La imagen `aspnet:10.0` trae `tzdata` (verificado: `/usr/share/zoneinfo/America/Bogota`).
- **Base de datos:** `db/003_fecha_nacimiento_hora_de_colombia.sql` reemplaza el `CHECK` por `fecha_nacimiento <= (now() AT TIME ZONE 'America/Bogota')::date`, de modo que el límite no cambia con el servidor, el contenedor ni la sesión. Se aplicó con el flujo normal del migrador sobre la base de desarrollo con datos: aplicó solo el `003`, los datos quedaron intactos y la aplicación funciona. **La zona está duplicada a propósito en dos sitios** (configuración de Negocios y este script, que no se edita): si alguna vez cambiara, hay que cambiar ambos, con un script nuevo.
- **Pruebas** (`ZonaHorariaTests` con `FakeTimeProvider`, 24 casos): el caso del hallazgo (2026-10-10 22:00 en Bogotá = 03:00 UTC del 11: el 11 se rechaza y el 10 se acepta); el **borde exacto de la medianoche de Bogotá** (04:59:59 UTC rechaza, 05:00:00 acepta); cambio de año y año bisiesto; el mismo instante con tres zonas (Bogotá, UTC y Kiritimati UTC+14) da tres «hoy» distintos; `Modificar` usa el mismo límite; resolución de la zona (por omisión, válida, inválida). En el host: zona inválida impide arrancar, y una zona distinta de la del equipo (`Asia/Tokyo`) llega al reloj. En la base: hoy en Bogotá se acepta y mañana se rechaza, el límite **no depende de la zona de la sesión** (`SET TIME ZONE` en Kiritimati, Pago_Pago y UTC) y la definición del `CHECK` fija `America/Bogota` y no usa `CURRENT_DATE`.
- **Rojo real antes de corregir:** con el `CHECK` anterior, una sesión en Kiritimati **aceptaba «mañana en Bogotá»**. Dos rojos «falsos» al principio fueron errores míos de la prueba (un `DateOnly` convertido a `DateTime`; y la aritmética de Kiritimati: a las 03:00 UTC allí son las 17:00 del *mismo* día).
- **Límite honesto de las pruebas de base de datos:** `now()` no se puede simular, y a media mañana «hoy en UTC» y «hoy en Bogotá» son la misma fecha, así que ningún test de comportamiento distingue UTC de Bogotá en la BD hasta la noche. Por eso hay además una prueba determinista de la **definición** del `CHECK`; el comportamiento nocturno de la aplicación lo cubren las pruebas con reloj falso.
- **Mutaciones** (7, todas detectadas; 34 de 34 acumuladas): «hoy» con UTC, límite un día más permisivo, zona por omisión UTC, proveedor que ignora la zona, host con el reloj del sistema (esta mutación **sobrevivía** al principio porque tu Windows ya está en hora de Colombia; la prueba de `Asia/Tokyo` la mata sin depender del equipo), `CHECK` con `CURRENT_DATE` y `CHECK` con UTC.
- **Nota de numeración:** el `003` lo tomó B6; el `CHECK` de nombre vacío (#41) pasa a ser el `004`.

**Hardening hecho (issue #15):**

- **B5 — sin secretos en el repositorio.** `Negocios/appsettings.json` ya no trae la cadena de conexión con contraseña. El host **no arranca** si falta `ConnectionStrings:Postgres` (nula, vacía o de solo espacios) y el mensaje indica cómo definirla (variable de entorno, `user-secrets` o un `appsettings.{Entorno}.json` local). Pruebas: el archivo no contiene `Password`, y el arranque con cadena ausente o vacía falla con ese mensaje.
- **B8 — WSDL solo en desarrollo.** `HttpGetEnabled = app.Environment.IsDevelopment()`. En producción `?wsdl`, `?singleWsdl` y `?xsd=xsd0` devuelven la **misma página de ayuda genérica de CoreWCF** (HTTP 200 sin contrato: no hay WSDL ni nombres del contrato); en desarrollo sí entregan el contrato. La prueba verifica que el contrato **no salga**, no un código HTTP de error. Se verificó además con el servicio real en ambos entornos.
- **B3 — healthchecks.** `negocios` y `web` ejecutan `bash` con `/dev/tcp` contra su propio `/health` y exigen una primera línea con ` 200 ` (la imagen base `aspnet:10.0`, Ubuntu 24.04, trae `bash`, `grep` y `head` pero **no** `curl`, `wget` ni `nc`, así que no hace falta instalar nada). El proxy usa `wget` contra la API local de Caddy (`127.0.0.1:2019`). `web` espera a `negocios` sano y el proxy a `web` sano; `docker compose up --wait` ya espera de verdad (todo sano en ≈25 s). Verificado con el **comando real** extraído de Compose: sano → 0; puerto cerrado, 404 y un servicio que responde 500 → distinto de 0. Límite: Docker **no reinicia** un contenedor `unhealthy` por sí solo; los healthchecks sirven para el orden de arranque, `--wait` y la visibilidad (el despliegue de la fase 7 falla y hace rollback si algo no queda sano).
- **B4 — cabeceras de seguridad.** En el `Caddyfile`, mediante un snippet `cabeceras_seguridad`: `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`/`Resource-Policy: same-origin`, `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` (sin `unsafe-inline`, sin `data:`, sin comodines) y se elimina `Server`.

**Trampas encontradas con B4 (por si se vuelve a tocar el Caddyfile):**

1. **El 401 salía sin ninguna cabecera.** Caddy descarta las cabeceras ya fijadas cuando un manejador devuelve un error (el 401 de `basic_auth` lo es), así que justo lo que ve un visitante sin credenciales no estaba protegido. Se probaron tres variantes con un Caddy mínimo: `header` en el sitio (falla), `header` diferido con `>` (falla y además deja `Server`) y **`handle_errors { import cabeceras_seguridad }` (correcta)**. También cubre un 502 con `web` caído (verificado). `WWW-Authenticate: Basic` se conserva.
2. **HSTS y `localhost`.** HSTS se aplica al *nombre de host sin importar el puerto*: enviarlo desde `https://localhost` haría que el navegador forzara HTTPS en `http://localhost:8080` y `:5080` (modos A y B). Por eso `HSTS_MAX_AGE` vale `31536000` (1 año) por omisión y `docker-compose.dev.yml` lo fija en `0` para el proxy. Para un primer despliegue prudente se puede bajar (p. ej. 300) en el `.env` del servidor.
3. **`asp-validation-summary` emite `<li style="display:none">`**, que una CSP estricta bloquea. Como no se usa validación en el cliente, se reemplazó por el parcial `Views/Shared/_ResumenErrores.cshtml`, que solo dibuja avisos cuando hay errores (y elimina un recuadro de error vacío que antes se emitía siempre).
4. **El `TestServer` no publica dirección base** y CoreWCF falla con «Could not find a base address»: las pruebas del host de negocios usan `UseUrls("http://localhost")` y el entorno `Production` explícito (en `Development` se cargan los `user-secrets` del equipo y falsearían la prueba).

**Verificación de B4:**

- `scripts/probar-acceso.ps1` revisa las cabeceras en **401, 401 de POST, `/health`, página, CSS, JS y 404**, y el máximo `max-age` de HSTS según el entorno (`-HstsEsperado`).
- Pruebas de vistas (`NingunaVistaEmiteNadaEnLineaNiApuntaAOtroOrigen`): sin `style=`, `<style>`, `<script>` en línea, manejadores de evento ni destinos de otro origen en `Consultar` (con usuario, vacía y con error), `Registrar`, `Editar` y `Error`.
- **`scripts/probar-navegador.ps1` (opcional: Node, Chrome o Edge y Docker; `puppeteer-core` 25.13.0 fijado con `package-lock.json` y vigilado por Dependabot):** en un navegador real, **ninguna violación de CSP** en `/`, `/Usuarios/Registrar` y `/Usuarios/Editar/12`; el CSS externo se aplica y `site.js` funciona; y un **control positivo** (una página servida con la misma política que viola a propósito) en la que el detector sí registra `style-src-elem`, `style-src-attr` y `script-src-elem`, para demostrar que el detector detecta. Pasa en Chrome 154 y en Edge.
- **Pruebas de mutación** de este PR (6): cadena vacía aceptada, WSDL también en producción, `/health` con comillas, parcial siempre dibujado, `style` en línea reintroducido y vuelta al resumen de ASP.NET → **las 6 detectadas** (con las 21 anteriores, 27 de 27).

### 4.9 Criterio de salida

`scripts/verificar.ps1` y el stack en Docker verdes; H1–H8 de alta y media cerrados con su PR; B1–B8 cerrados o en backlog con motivo.

---

## Fase 5. Integración continua ✅

Rama `ci/integracion-continua`, PR #51 (issue #20). [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) corre en `pull_request` hacia `develop` y `main` y en `push` a ambas, con permisos mínimos (`contents: read`), cancelación de ejecuciones obsoletas y tiempo máximo por trabajo. **Tres trabajos independientes** (un PR muestra de una vez todo lo que falla):

| Trabajo | Qué ejecuta | Tiempo |
|---|---|---|
| `ci` | `scripts/verificar.ps1` (restore, `dotnet format --verify-no-changes`, compilación con `TreatWarningsAsErrors` y las 302 pruebas, incluidas las de integración con PostgreSQL en Docker), `scripts/cobertura.ps1` (mínimo 80 % en Negocios), `dotnet list package --vulnerable --include-transitive` y sube el informe de cobertura como artefacto | ≈ 2 min |
| `migrador` | `shellcheck db/migrate.sh` y `scripts/probar-migrador.ps1` (9 escenarios sobre una base desechable) | ≈ 30 s |
| `docker` | `docker compose config -q` con `.env.example`, hadolint sobre los dos Dockerfile, construcción de las dos imágenes y Trivy (CRITICAL y HIGH con arreglo disponible) | ≈ 1 min |

*(En la fase 7 se añadió un cuarto trabajo, `despliegue`, y el trabajo `docker` valida además el compose de producción.)*

Decisiones y por qué:

- **El CI ejecuta los mismos scripts que se corren en local** (`verificar.ps1`, `cobertura.ps1`, `probar-migrador.ps1`, con `pwsh` del runner) en vez de repetir los comandos en YAML. Así «lo que CI ejecuta se puede ejecutar idéntico en local» y hay un solo lugar donde mantenerlo.
- **Acciones fijadas por SHA completo** (con la versión en un comentario), no por etiqueta: una etiqueta se puede mover, un SHA no. Dependabot (`github-actions`, reactivado en esta fase) propone las actualizaciones. Las versiones vigentes (`checkout` v7, `setup-dotnet` v6, `cache` v6, `upload-artifact` v7, `trivy-action` v0.36, `hadolint-action` v3.5) eran más nuevas que las del borrador original de este plan; se consultaron con `gh api`, no se supusieron.
- **Los nombres `ci`, `migrador` y `docker`** son los checks que se marcarían obligatorios si el repositorio pasara a un plan con protección de ramas ([ADR 0005](adr/0005-flujo-de-ramas-y-proteccion-en-github-free.md)). Hoy no bloquean el botón de fusionar: se respeta por disciplina.
- **Sin CodeQL:** el análisis de código está desactivado en el repositorio (la API responde 403 «not enabled») y no se verificó si el plan Free lo permite en repositorios privados. El análisis estático de C# ya lo hace SonarCloud en cada PR (#24).

Verificación de la fase (antes de fusionar):

1. **Localmente, con las mismas herramientas:** `actionlint` (sin avisos), hadolint, shellcheck, `dotnet list package --vulnerable` y Trivy sobre las dos imágenes (0 vulnerabilidades).
2. **En un contenedor Linux** (SDK 10.0.401, sobre `git archive` para tener los finales de línea de un clon): format, compilación y pruebas. **Encontró dos fallos que en Windows no existían**:
   - `BogotaEstaSiempreEnUtcMenosCincoSinHorarioDeVerano` afirmaba `SupportsDaylightSavingTime == false`, pero en Linux esa propiedad también es verdadera si la zona tuvo horario de verano *alguna vez* (Colombia lo aplicó en 1992-93). Ahora comprueba el desplazamiento real de cada mes de 2026 a 2036.
   - Las 9 pruebas de contrato que usan un servidor real fallaban porque la imagen del SDK define `ASPNETCORE_HTTP_PORTS` y el servidor terminaba escuchando en `http://[::]:8080`, que no sirve como destino. El fixture ahora fija `http://127.0.0.1:0` y lee la dirección real asignada, sea cual sea el entorno.
   - Las pruebas de integración **no** pueden correr *dentro* de un contenedor (Testcontainers no arranca su «reaper» en Docker anidado); su validación en Linux es la del propio CI.
3. **Hadolint** marcaba dos hallazgos de nivel «info» en los Dockerfile: `USER $APP_UID` (falso positivo: el UID numérico lo define la imagen base de Microsoft; se ignora esa línea con un comentario que lo explica) y una variable sin comillas en `chown` (corregida).
4. **En GitHub:** los tres trabajos en verde a la primera, con el log confirmando que las pruebas corrieron (15 + 106 + 127 + 54 en Linux), la cobertura (Negocios 100 %, Presentación 95,7 %), el escaneo de Trivy y los 9 escenarios del migrador.
5. **Control negativo** (en el mismo PR, ya revertido): una prueba rota, un Dockerfile con `apt-get install` sin versión y una variable sin comillas en `migrate.sh` pusieron en **rojo** exactamente `ci`, `docker` y `migrador`, cada uno por su causa (la prueba, hadolint DL3008 y shellcheck SC2086). Un CI que nunca se ha visto fallar no demuestra nada.

Para el repositorio se hicieron portables a Linux `scripts/probar-migrador.ps1` y `scripts/cobertura.ps1` (rutas con `/` en lugar de `\`). Sigue pendiente, para la fase 7, la validación de `docker-compose.prod.yml` (y que no declare `ports`) en el trabajo `docker`.

---

## Fase 6. Preparar el VPS compartido ⏳

El VPS **ya existe y aloja otros proyectos** (PortalCV y Colfe). Aquí no se instala ni se endurece desde cero: se **audita**, se añade un proyecto más y cada cambio a lo compartido (nginx, daemon de Docker, firewall, SSH) se trata aparte, con ventana y aprobación. Decisión y alternativas en [ADR 0011](adr/0011-despliegue-detras-del-nginx-compartido.md) (aceptada).

Los comandos del servidor los ejecuta quien administra el VPS por SSH y pega la salida; el proyecto se llama `gdb-crud` en el servidor (subdominio, carpeta y backups) y su alias de red es `gdb-crud-web`, como `colfe-web` en Colfe.

**Estado verificado por SSH el 2026-10-10:**

| Hecho | Consecuencia para este proyecto |
|---|---|
| Contabo, 6 vCPU, 12 GB, disco 193 GB con 177 GB libres (9 %), usuario `maomauro` | Holgura de sobra; un solo usuario con acceso a Docker |
| `portalcv-nginx-prod` es el único contenedor que publica 80/443 (IPv4 e IPv6) | **Caddy no puede publicar 80/443**: entra por ese nginx |
| Red compartida `curriculum-vitae-web_portalcv-net-prod` con `portalcv-nginx-prod`, `portalcv-backend-prod`, `portalcv-mariadb-prod` y `colfe-web-1` | Solo nuestro `proxy` se une a ella, con un alias; `db`, `negocios` y `web` quedan en la red interna del proyecto |
| `docker/colfe.conf` usa `resolver 127.0.0.11` + `set $upstream` | Es el patrón obligatorio para nuestro vhost |
| Cloudflare delante: la IP real llega en `CF-Connecting-IP`; certificados de origen en `/etc/nginx/ssl/` | Sin Let's Encrypt; el certificado de origen se instala a mano |
| `/srv/sitiosapps/_backups/{colfe,portalcv,premigracion}` y dos tareas de cron (PortalCV el último día del mes, Colfe los domingos) | Un directorio `gdb` más y una tarea diaria que no choque con esas horas |

### 6.1 Auditoría de solo lectura

Registrar la salida en el PR de esta fase. Lo que falte (SSH con contraseña, sin `fail2ban`, sin actualizaciones automáticas…) es un issue aparte: no se corrige «de paso» en un servidor compartido.

```bash
sudo sshd -T | grep -Ei 'permitrootlogin|passwordauthentication|allowusers'
sudo ufw status verbose
systemctl is-active fail2ban unattended-upgrades
cat /etc/docker/daemon.json 2>/dev/null; docker compose version
timedatectl | grep -i 'time zone'; free -h
```

`docker compose version` debe ser ≥ 2.24 (el override usa `!reset`).

**Resultado (2026-10-11):**

| Comprobación | Resultado | Conclusión |
|---|---|---|
| SSH | `permitrootlogin no`, `passwordauthentication no`; sin `AllowUsers` | Correcto. Sin `AllowUsers` entra cualquier usuario con llave; hoy solo existe `maomauro` |
| Firewall (`ufw`) | Activo, entrada denegada por omisión; 22 abierto; **80 y 443 solo desde los rangos de Cloudflare** (IPv4 e IPv6) | Correcto: el origen no se puede alcanzar directamente desde internet |
| `fail2ban`, `unattended-upgrades` | Ambos activos | Correcto |
| `/etc/docker/daemon.json` | No existe | Sin rotación global de logs; este proyecto los limita por servicio (7.1). No se toca el daemon (reiniciarlo reinicia a todos) |
| Docker Compose | v5.5.1 | Cumple el mínimo (2.24) para `!reset` |
| Memoria | 11 GiB, 8,9 GiB libres, **sin swap** | Sobra para ~400 MB de este stack; la falta de swap es una observación para el servidor |
| Zona horaria del servidor | `Europe/Berlin` | No afecta a la aplicación (el contenedor corre en UTC y «hoy» es `America/Bogota` por configuración, ADR 0006). **Sí afecta a las horas del `cron`**, que son hora de Berlín |
| Contenedores | Los 6 arriba (Colfe 2 días, PortalCV 3 días) | Línea base para comprobar, después de cada paso, que nada se reinició |

Observaciones para quien administra el servidor (no se corrigen dentro de este plan, ver el principio de la fase):

1. **Los puertos publicados por Docker pueden saltarse `ufw`** (Docker agrega sus propias reglas de `iptables`). Si es así, la restricción de 80 y 443 a Cloudflare no se cumple para el contenedor del nginx. No está verificado: una comprobación de solo lectura es `sudo iptables -S DOCKER-USER` y, desde fuera de Cloudflare, `curl -k --resolve <dominio>:443:13.140.188.159 https://<dominio>/`.
2. Sin rotación global de logs de Docker ni swap: el riesgo es llenar el disco o quedarse sin memoria por un proyecto ajeno.

### 6.2 DNS y certificado en Cloudflare

El orden importa: **el certificado y el modo SSL se preparan primero, y el registro DNS se crea al final (paso del vhost, 6.6)**. Si el nombre ya apuntara al servidor antes de que el nginx tenga el vhost, Cloudflare enviaría las peticiones a un nginx que no conoce ese nombre.

1. Comprobar el modo SSL/TLS de la zona; debe ser **Full (strict)** (lo usan los demás proyectos). **No cambiarlo** si es otro: afectaría a los demás; se anota y se decide aparte.
2. Certificado de Origen para `gdb-crud.sitiosapps.com` (solo ese nombre, no un comodín: un par `.pem`/`.key` por subdominio, como el resto): guardar `/etc/nginx/ssl/gdb-crud.sitiosapps.com.pem` y `.key` (la llave con permisos `600`; la pega quien administra, nunca pasa por el repo ni por el chat). Anotar la fecha de vencimiento.
3. **Al final, con el vhost ya activo (6.6):** registro `A` `gdb-crud.sitiosapps.com` → `13.140.188.159`, **con proxy de Cloudflare activo**.

### 6.3 Carpetas y configuración del proyecto

```bash
mkdir -p /srv/sitiosapps/gdb-crud/deploy /srv/sitiosapps/_backups/gdb-crud
chmod 750 /srv/sitiosapps/gdb-crud /srv/sitiosapps/gdb-crud/deploy
chmod 700 /srv/sitiosapps/_backups/gdb-crud
```

`/srv/sitiosapps/gdb-crud/.env` (permisos `600`, **solo en el servidor**; `$` en comillas simples). Permisos del resto: `deploy/*.sh` `750`; backups y su carpeta `600` y `700`; llave del certificado `600` (dueño root) y certificado `644`; `~/.ssh` `700` y `authorized_keys` `600`.



```
DOMINIO=gdb-crud.sitiosapps.com
POSTGRES_DB=usuarios_db
POSTGRES_USER=usuarios_admin
POSTGRES_PASSWORD=<openssl rand -hex 24>
APP_DB_USER=usuarios_app
APP_DB_PASSWORD=<openssl rand -hex 24>
AUTH_USUARIO=admin
AUTH_HASH='<hash generado con scripts/generar-hash-auth.ps1, entre comillas simples>'
IMAGE_TAG=latest
GITHUB_REPOSITORY=maomauro/grupodigitalbank-crud
```

Las contraseñas se generan en **hexadecimal** (`openssl rand -hex 24`), no en base64: sin `/`, `+`, `=` ni `;`, que romperían la cadena de conexión de Npgsql o la interpolación de Compose. `GITHUB_REPOSITORY` va en **minúsculas** (GHCR no admite mayúsculas). No hace falta iniciar sesión en GHCR en el servidor ni guardar un token: el despliegue usa el `GITHUB_TOKEN` de cada ejecución (7.2).

### 6.4 Llave de despliegue exclusiva y restringida

Hoy el único usuario con Docker es `maomauro`, y pertenecer al grupo `docker` equivale a ser root sobre un host que contiene también las bases de PortalCV y Colfe. Por eso la llave de GitHub Actions **no abre una shell**:

1. En el equipo local: `ssh-keygen -t ed25519 -f $HOME\.ssh\gdb_deploy -C "gdb-deploy"` (sin passphrase: la usará Actions).
2. En `~/.ssh/authorized_keys` del servidor, una línea con restricciones delante de la clave pública:

   ```
   restrict,command="/usr/local/sbin/gdb-crud-entrada.sh" ssh-ed25519 AAAA... gdb-deploy
   ```

3. `gdb-crud-entrada.sh` (versionado en el repo como `deploy/entrada.sh`; los cinco scripts de `deploy/` se instalan **a mano** con `sudo bash deploy/instalar.sh`, que los deja en `/usr/local/sbin/` con dueño `root` y permiso `755`, **fuera** de la carpeta donde se extrae el paquete: así un paquete no puede reemplazarlos; se actualizan solo por un PR revisado y copia manual, [ADR 0013](adr/0013-politica-de-seguridad-del-despliegue.md)) lee `$SSH_ORIGINAL_COMMAND`, acepta **solo** `desplegar <tag>` con el tag validado por expresión regular, extrae el paquete recibido por la entrada estándar en `/srv/sitiosapps/gdb-crud` y ejecuta `desplegar.sh <tag>`. Cualquier otra orden se rechaza.

Verificar desde otra terminal que la llave **no** puede ejecutar `ls` ni abrir una shell. No se desactiva la verificación de host: el `known_hosts` se fija con `ssh-keyscan`.

### 6.5 Docker y límites

Docker ya está instalado. **No se edita `/etc/docker/daemon.json`**: reiniciar el daemon reinicia PortalCV y Colfe. El límite de logs (`json-file`, `10m` × `5`) y `no-new-privileges` se declaran por servicio en el compose de este proyecto (7.1). Un límite global es una tarea aparte, con ventana.

### 6.6 Vhost en el nginx compartido (PR en `Curriculum-Vitae-Web`)

Cambio en **otro repositorio y sobre infraestructura de producción compartida**: se hace en un PR propio, con aprobación explícita y en una ventana. Añadir un volumen exige **recrear** `portalcv-nginx-prod`, unos segundos sin servicio para PortalCV y Colfe.

1. `docker/gdb-crud.conf`: **es una copia exacta de [`deploy/nginx/gdb-crud.conf`](../deploy/nginx/gdb-crud.conf)**, la fuente de verdad de este repositorio (la que el entorno local usa tal cual con el servicio `borde`). Sigue la estructura de `docker/colfe.conf`: redirección de 80 a 443, el certificado de origen de `/etc/nginx/ssl`, `server_tokens off`, **sin HSTS** (lo agrega el Caddyfile; duplicarlo repetiría la cabecera), `resolver 127.0.0.11` + `set $gdb_crud_upstream http://gdb-crud-web:80` (el nombre se resuelve en cada petición, así un contenedor ausente no tumba el nginx compartido) y `X-Forwarded-For $http_cf_connecting_ip` + `X-Forwarded-Proto $scheme`.
2. Montarlo como volumen del servicio `nginx` en `docker-compose.prod.yml` de PortalCV.
3. Antes de recrear: `nginx -t` en un contenedor desechable con la **misma imagen** y todos los `conf.d` (incluido el nuevo), y comprobar que los certificados existen. Antes y después, `curl -I` a PortalCV y a Colfe.
4. Mientras el stack de este proyecto no esté desplegado el vhost responde 502, sin afectar a los demás: por eso el patrón con `resolver`.

### 6.7 Conectar GitHub con el VPS

- *Environments → `production`*: creado el 2026-10-11 con la restricción **Deployment branches and tags → solo `main`** (verificada: disponible en privados Free; ver la corrección en 2.3). Así solo el código de `main`, que se fusiona a mano, puede leer los secretos de producción. *Revisores requeridos* no se ha comprobado y no se usa. Además, `entrega` solo corre en un push a `main`.
- Secretos del environment:

  | Secreto | Contenido |
  |---|---|
  | `VPS_HOST` | `13.140.188.159` |
  | `VPS_PORT` | Puerto SSH |
  | `VPS_USER` | `maomauro` |
  | `VPS_SSH_KEY` | Llave privada `gdb_deploy` (solo puede ejecutar `gdb-crud-entrada.sh`) |
  | `VPS_KNOWN_HOSTS` | Salida de `ssh-keyscan -p <puerto> <host>` |

  Las contraseñas de la app **no** van en GitHub: viven en `/srv/sitiosapps/gdb-crud/.env`.
- *Packages*: la etiqueta `org.opencontainers.image.source` con que se publican las imágenes las **vincula al repositorio** y siguen privadas; así el `GITHUB_TOKEN` del flujo puede leerlas. Tras la primera publicación, comprobarlo en la página del paquete (*Package settings*).

---

## Fase 7. Entrega continua ✅

Rama `ci/despliegue-vps`. El mecanismo está **construido y probado de punta a punta contra un servidor simulado**; la primera publicación real es la fase 8. Decisiones: [ADR 0011](adr/0011-despliegue-detras-del-nginx-compartido.md) (entrar por el nginx compartido) y [ADR 0013](adr/0013-politica-de-seguridad-del-despliegue.md) (política de seguridad del paquete de despliegue).

### 7.1 Compose, Caddyfile y entorno local

- **El compose base ya no publica puertos** (los `ports` del `proxy` desaparecieron). El trabajo `docker` del CI comprueba que `docker-compose.yml` + `docker-compose.prod.yml` no contengan `ports:`.
- **`Caddyfile`:** la dirección del sitio es `http://{$DOMINIO}` (sin TLS ni certificados), con `trusted_proxies static private_ranges` para conservar el `X-Forwarded-Proto` del nginx y un comodín `:80` que responde 404 a cualquier otro nombre de host (otro contenedor de la red compartida no recibe la aplicación ni pidiendo credenciales).
- **`docker-compose.prod.yml`:** imágenes `${REGISTRO:-ghcr.io}/${GITHUB_REPOSITORY}/{negocios,web}:${IMAGE_TAG}` (sin `build`), el `proxy` unido a la red compartida `curriculum-vitae-web_portalcv-net-prod` con el alias `gdb-crud-web` (solo él; `db`, `negocios` y `web` quedan en la red interna), límite de logs (`json-file`, 10 MB × 5) y `no-new-privileges` en todo. No se toca `daemon.json`.
- **Entorno local fiel a producción:** el perfil `con-proxy` del compose de desarrollo agrega el servicio `borde`, un nginx con HTTPS autofirmado y **el mismo vhost** que irá a producción (`deploy/nginx/gdb-crud.conf`, fuente de verdad que se copia al repositorio de PortalCV, 6.6). Así `probar-acceso.ps1` y `probar-navegador.ps1` prueban la cadena nginx → Caddy → aplicación **antes** de desplegar.
- **Hallazgo de la cadena, ya corregido:** ni la cookie antiforgery ni la de TempData llevaban `Secure`, ni siquiera con `X-Forwarded-Proto: https`. La aplicación ahora usa `CookieSecurePolicy.SameAsRequest` (Secure con HTTPS; sin Secure en `http://localhost:8080`, donde una cookie Secure se descartaría). 5 pruebas nuevas; `probar-acceso.ps1` suma dos comprobaciones (HSTS una sola vez y cookie Secure) y quitar `trusted_proxies` del Caddyfile hace fallar exactamente la segunda. Además: `server_tokens off` en el vhost, y el script ahora acepta que `Server` lo ponga el proxy de delante (sin versión).

### 7.2 Mecanismo de despliegue (`deploy/`)

| Archivo (instalado en `/usr/local/sbin/`, dueño root) | Qué hace |
|---|---|
| `entrada.sh` (`gdb-crud-entrada.sh`) | Lo único que ejecuta la llave (`restrict,command=`). Solo acepta `desplegar <tag>`; lee el token de GHCR de la primera línea de la entrada estándar y el paquete `.tar.gz` del resto; **revisa el paquete antes de extraerlo** (solo archivos regulares con nombres de una lista cerrada); inicia sesión en GHCR en una carpeta de configuración de Docker temporal que borra al terminar |
| `desplegar.sh` | Instala la entrega como `releases/<tag>/`, la valida con la política, hace una copia de la base si ya corre, descarga las imágenes, levanta con `up -d --wait` (el migrador corre antes que Negocios) y, si falla, **vuelve a la versión anterior** (las migraciones no se deshacen: expand/contract, ADR 0007). Bloqueo contra despliegues simultáneos; conserva las 5 últimas versiones; nunca escribe el `.env` (el tag viaja por `IMAGE_TAG` en el entorno) |
| `politica.py` | Lista blanca sobre el compose ya resuelto (ADR 0013): servicios, imágenes, montajes, redes y opciones peligrosas. Lee el JSON por la **entrada estándar** (no abre ninguna ruta recibida por argumento) |
| `backup.sh` | `pg_dump` comprimido con suma SHA-256, verificación de integridad, permisos 600, rotación (14 días) y copia externa opcional con `rclone`. Patrón de Colfe |
| `probar-restauracion.sh` | Restaura la última copia en un PostgreSQL desechable y comprueba tabla, funciones y migraciones; falla con una copia corrupta o inexistente |
| `instalar.sh` | Instala los cinco anteriores con dueño root y muestra sus sumas SHA-256 |
| `nginx/gdb-crud.conf` | El vhost para el nginx compartido (6.6) |

El **token** para descargar las imágenes es el `GITHUB_TOKEN` de cada ejecución (con `packages: read`): no hay un PAT en el servidor. Requiere que el paquete esté vinculado al repositorio, lo que hace la etiqueta `org.opencontainers.image.source` con que se publican las imágenes.

### 7.3 Workflow `.github/workflows/deploy.yml`

- **Disparo (D7):** es un **trabajo más del propio `ci.yml`**: `entrega` llama a `deploy.yml` (*workflow reutilizable*) solo en un **push a `main`** (el merge de `develop`) y únicamente si `ci`, `migrador`, `docker` y `despliegue` terminaron en verde (`needs`). Además, `deploy.yml` tiene `workflow_dispatch` con el tag de una versión ya publicada (rollback o redespliegue). Los merges a `develop` ejecutan CI pero **no despliegan**. La concurrencia del CI **no cancela** las ejecuciones de `main` (cancelaría un despliegue a mitad de camino).
- **Por qué no `workflow_run`** (primer diseño): ejecuta el archivo de la rama *por defecto* (`develop`) y obliga a hacer checkout de un SHA ajeno al evento (`head_sha`), el patrón de «código no confiable en un flujo con secretos». Aquí la condición lo acotaba a `main`, pero un flujo único no necesita ninguna de las dos cosas y se ve entero en una sola ejecución. *(Ver abajo: no era lo que SonarCloud marcaba.)*
- **`imagenes`:** construye y publica `negocios` y `web` en GHCR con las etiquetas `sha-<12 hex>` y `latest` (nombre en minúsculas), con caché de GitHub Actions.
- **`desplegar`** (*environment* `production`, `concurrency: deploy-production` a nivel de trabajo y sin cancelar): toma **el código de esa versión** (compose, `Caddyfile` y migraciones deben corresponder exactamente a lo que se despliega, también al volver atrás), arma el paquete, configura SSH con `StrictHostKeyChecking=yes` y `known_hosts` fijado (nunca se desactiva la verificación del host) y ejecuta `desplegar <tag>` pasando el token por la entrada estándar.
- **Prueba de humo** como la vería un usuario (por Cloudflare y el nginx compartido): `/health` responde `ok`, HSTS presente y la raíz exige credenciales (401).
- Acciones fijadas por SHA; `actionlint` sin avisos.

### 7.4 Rollback

- **Automático** si el despliegue falla antes de dar por buena la versión nueva (descarga, migrador, healthchecks): `desplegar.sh` levanta de nuevo la anterior. Si es el primer despliegue, detiene el stack conservando los volúmenes.
- **Tras una prueba de humo fallida** el despliegue queda en rojo y el rollback es **manual**: lanzar el workflow `deploy` con el tag anterior (`workflow_dispatch`).
- **Esquema:** volver de versión no deshace migraciones; se diseñan *expand/contract*. Si hay que revertir datos, se restaura la copia tomada justo antes (`gdb-crud-probar-restauracion.sh` prueba que se puede).

### 7.5 Verificación

- **`scripts/probar-despliegue.sh`** (`.ps1` en Windows; ≈ 7 min; es el trabajo `despliegue` del CI): un servidor simulado con registro local, red compartida y nginx con el vhost real delante. **75 comprobaciones**: 13 órdenes rechazadas; 6 paquetes peligrosos rechazados antes de extraerse (enlace, `..`, ruta absoluta, archivo extra, subcarpeta, paquete incompleto); **12 composes maliciosos rechazados** (privilegiado, monta `/`, monta el socket de Docker, publica puertos, red del host, otro registro, alias ajeno, `web` en la red compartida, servicio extra, volumen ajeno, `build`, `cap_add`); despliegue v1 sano, **sin ningún puerto publicado**, solo el `proxy` en la red compartida con su alias, y el token sin dejar credenciales; despliegue v2 con **copia previa**; **despliegue v3 roto que vuelve solo a v2** y el sitio sigue respondiendo; bloqueo concurrente; versión inexistente sin tocar nada; un nombre de host ajeno recibe 404; backup con suma, permisos 600 y restauración válida, y copias corruptas, inexistentes, sin esquema o sin funciones que **no** pasan.
- **Lo que solo mostró el runner real:** el trabajo `despliegue` falló la primera vez en GitHub (30 comprobaciones) y no en mi máquina, porque los runners definen `GITHUB_REPOSITORY` (con mayúsculas, `maomauro/GrupoDigitalBank-CRUD`) y en Compose el entorno gana al `.env`: las imágenes salieron con otro nombre que el que valida la política, que las rechazó con razón. En el servidor real esa variable no existe, pero el mecanismo no debe depender de eso: `desplegar.sh` y `backup.sh` hacen `unset GITHUB_REPOSITORY`, de modo que el `.env` es la única fuente. Se reprodujo y se verificó localmente pasando esa variable al contenedor de prueba.
- **SonarCloud, y una hipótesis equivocada:** el *C Security Rating on New Code* lo atribuí a `deploy.yml` (porque la captura que tenía mostraba ese archivo) y probé dos arreglos que no lo resolvieron. El detalle real estaba en las **anotaciones del check de GitHub** (`gh api …/check-runs/<id>/annotations`), que SonarCloud publica aunque el proyecto sea privado: la vulnerabilidad era `deploy/politica.py`, *Path Traversal* (la ruta del JSON llegaba por la línea de comandos y se abría). Ahora la política lee el compose por la entrada estándar. El resto eran avisos de estilo, atendidos en `deploy/`: `[[` en lugar de `[`, parámetros posicionales a variables locales, la complejidad cognitiva de `revisar()` (66 → funciones pequeñas) y la palabra «TODO» (en español, «todo») que Sonar toma por una tarea pendiente. Lección: **mirar las anotaciones antes de suponer**.
- **Honestidad sobre el proceso:** la prueba tardó cuatro ejecuciones en pasar y **ninguno de los defectos estaba en el mecanismo**: `/tmp` montado sin permiso de ejecución en la VM de Docker Desktop (se llama a los scripts con `bash`, que además es más robusto), una comprobación mía que buscaba «db» en nombres que contienen «gdb», la cabecera `Host` con que se probaba el nginx (cayó en el comodín `:80`, que así demostró funcionar) y una cabecera `Host` duplicada.
- **Control negativo de la propia prueba** (6 mutaciones sobre una copia del repositorio, **todas detectadas**): una política que deja pasar todo (los 12 composes maliciosos *se desplegaron de verdad* y la prueba lo marcó), sin rollback, una entrada que acepta cualquier orden, una entrada que admite enlaces simbólicos, sin copia previa al desplegar y una restauración que da todo por bueno. **Dos sobrevivieron al primer intento y señalaron huecos de la prueba, no del mecanismo:** faltaban órdenes de dos palabras con un tag válido (`otro v1`; con la comprobación de la primera palabra anulada, las demás se rechazaban igualmente por el tag) y faltaba una copia válida pero incompleta (tablas sin funciones) para ejercitar la comprobación final de la restauración, porque una copia corrupta falla antes. Se añadieron ambos casos (la prueba pasó de 69 a 75 comprobaciones) y las dos mutaciones pasaron a ser detectadas.
- `shellcheck` limpio en todos los scripts (también en el CI), `actionlint` sin avisos y la política compila con `py_compile`.

---

## Fase 8. Primera publicación ⏳

1. SSL en Full (strict) comprobado y certificado de origen instalado (6.2, puntos 1 y 2). El registro DNS **todavía no**: se crea en el punto 3.
2. Auditoría del VPS registrada (6.1), `/srv/sitiosapps/gdb-crud/.env` completo y llave restringida probada (6.3, 6.4).
3. PR de `docker/gdb-crud.conf` fusionado en `Curriculum-Vitae-Web` y nginx recreado en la ventana acordada (6.6); PortalCV y Colfe responden igual que antes. **Solo entonces** se crea el registro DNS (6.2, punto 3): hasta el primer despliegue el nombre responde 502, sin afectar a los demás.
4. Secretos y environment `production` creados (6.7).
5. `develop` con las fases 3 a 5 y 7.1 fusionadas y CI verde; PR de release `develop` → `main` (título `release: v1.0.0`), fusionado con **merge commit**. Ese merge dispara el despliegue.
6. En el VPS: `docker compose -p gdb-crud ps` todo `healthy`; `docker ps` confirma que los contenedores de PortalCV y Colfe no se reiniciaron (tiempo de actividad intacto).
7. Verificación externa:
   - `https://gdb-crud.sitiosapps.com` pide usuario y clave.
   - Registrar → consultar → modificar → eliminar.
   - `http://gdb-crud.sitiosapps.com` redirige a HTTPS.
   - La IP real del cliente llega a la aplicación (cabeceras reenviadas, 7.1) y las cookies son las esperadas.
   - Solo responden los puertos que el servidor ya exponía (22, 80, 443); el 5432 no responde.
   - securityheaders.com en nota A (SSL Labs evalúa el borde de Cloudflare, no nuestro certificado).
   - El nombre `x');alert(1);//` no ejecuta nada.
8. Prueba de reinicio: `docker restart` de los servicios de este proyecto y comprobar que vuelven sanos. Reiniciar todo el VPS afecta a los tres proyectos: se hace una vez, en ventana acordada, y se comprueba que cada proyecto vuelve solo.
9. Tag `v1.0.0` y release en GitHub.

---

## Fase 9. Operación ⏳

### 9.1 Backups de PostgreSQL

Se parte del patrón maduro de Colfe (`deploy/backup.sh` en su repo; **leerlo antes de adaptarlo**): volcado no interactivo con `docker compose exec -T`, `gzip`, `sha256sum`, verificación de integridad, rotación por `RETENTION_DAYS`, copia externa opcional con `rclone` y un `probar_restauracion.sh`. Nuestra versión es `deploy/backup.sh` (versionada; la copia el despliegue) y la ejecutan el cron y también `desplegar.sh` antes de cada despliegue. No se hace `source .env` (los `$$` del hash se expandirían); se usan las variables del propio contenedor:

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /srv/sitiosapps/gdb-crud
umask 077   # los volcados solo los lee el dueño
DESTINO="${BACKUP_DIR:-/srv/sitiosapps/_backups/gdb-crud}"
F="$DESTINO/usuarios_$(date +%F_%H%M).sql.gz"
docker compose -p gdb-crud -f docker-compose.yml -f docker-compose.prod.yml exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner' | gzip > "$F"
gzip -t "$F" && sha256sum "$F" > "$F.sha256"
find "$DESTINO" -name 'usuarios_*.sql.gz*' -mtime +"${RETENTION_DAYS:-14}" -delete
```

- Cron del usuario `maomauro`, a una hora que no choque con las existentes (PortalCV: último día del mes 03:00; Colfe: domingos 02:15): `45 2 * * * BACKUP_DIR=/srv/sitiosapps/_backups/gdb-crud RETENTION_DAYS=14 /srv/sitiosapps/gdb-crud/deploy/backup.sh >> /srv/sitiosapps/_backups/gdb-crud/backup.log 2>&1`.
- **Un cron documentado no es un cron instalado** (ya falló en silencio en PortalCV): verificar `crontab -l`, que el script exista con permiso de ejecución y correrlo a mano una vez antes de dar el backup por resuelto.
- Copia **fuera del servidor** (regla 3-2-1): `rclone` a un bucket (Backblaze B2, S3, Drive) o `rsync` a otra máquina.
- **Probar la restauración** al menos una vez en un contenedor desechable (`deploy/probar_restauracion.sh`): `gunzip -c backup.sql.gz | docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'`. Un backup sin restauración probada no cuenta.

### 9.2 Monitoreo

- Uptime externo (UptimeRobot / Better Stack) sobre `https://gdb-crud.sitiosapps.com/health`, con alerta a tu correo.
- Alerta de espacio en disco (`df`): el disco es compartido por los tres proyectos (9 % usado al 2026-10-10).
- Logs: `docker compose -p gdb-crud logs --since 1h`; rotación limitada por servicio en el compose (7.1).

### 9.3 Mantenimiento

| Tarea | Frecuencia |
|---|---|
| Revisar y fusionar PR de Dependabot | Semanal |
| `apt upgrade` y reinicio si cambia el kernel | Según lo que arroje la auditoría 6.1; el reinicio se coordina con PortalCV y Colfe |
| Rotar contraseñas de BD (y el hash de acceso) y la llave de despliegue | Cada 6 meses (ya no hay PAT de GHCR: se usa el `GITHUB_TOKEN` de cada ejecución) |
| Probar restauración de backup | Trimestral |
| Certificado de origen de Cloudflare | Anotar el vencimiento al crearlo (6.2) y renovarlo antes; la alerta de uptime avisa si falla |
| Fuerza bruta sobre el Basic Auth (ADR 0004) | Regla de límite de tasa en Cloudflare para `gdb-crud.sitiosapps.com`; `fail2ban` sobre el log de Caddy ya no sirve porque Caddy solo ve la IP de nginx |
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
- [x] CI en cada PR (format, build, tests, cobertura, vulnerabilidades, shellcheck, hadolint, Trivy y migrador); visto en rojo y en verde. «Obligatorio» no es posible en este plan (ADR 0005): se respeta por disciplina
- [ ] VPS compartido: auditoría de solo lectura registrada (SSH por llave, firewall, fail2ban, actualizaciones) y llave de despliegue restringida a `gdb-crud-entrada.sh`
- [ ] DNS en Cloudflare + HTTPS válido (Full strict, certificado de origen) con HSTS y cabeceras de seguridad; PortalCV y Colfe intactos tras el cambio en el nginx
- [ ] Aplicación protegida con autenticación
- [ ] BD solo en red interna y con rol de mínimo privilegio
- [ ] Despliegue automático con backup previo, smoke test y rollback probado (provocar un fallo a propósito)
- [ ] Backup diario, copia externa y restauración probada
- [ ] Monitoreo externo activo y alerta recibida en una prueba
- [ ] Reinicio coordinado del VPS (con PortalCV y Colfe) sin intervención manual
- [ ] Documentación y runbook completos

## Riesgos y dependencias

| Riesgo | Mitigación |
|---|---|
| El proyecto nunca se ha compilado | Fase 3.2 antes de cualquier cambio de código |
| GitHub Free no protege ramas en repos privados | Verificar en 2.3; alternativas: Pro, repo público o hook + disciplina, con ADR |
| Un vhost mal escrito tumba el nginx compartido (y con él PortalCV y Colfe) | Patrón `resolver` + `set $upstream`; `nginx -t` en un contenedor desechable con la misma imagen antes de recrear; PR propio y ventana acordada (6.6) |
| Recrear `portalcv-nginx-prod` corta unos segundos a los otros proyectos | Una sola vez, en ventana; el vhost no se toca en cada despliegue |
| Cambiar `daemon.json`, firewall o SSH del VPS afecta a todos los proyectos | No se hace dentro de este plan; si la auditoría (6.1) lo pide, issue aparte |
| Cabeceras reenviadas mal con dos proxies en cadena (IP o esquema erróneos) | `trusted_proxies` en Caddy y prueba local con un nginx delante antes de desplegar (7.1) |
| Perder datos en una migración | Backup obligatorio antes de cada despliegue; migraciones *expand/contract* |
| Llave de despliegue comprometida | Llave exclusiva con `restrict,command=` que solo ejecuta `gdb-crud-entrada.sh` (no abre shell, aunque `maomauro` esté en el grupo `docker`), rotación semestral |
| Docker salta `ufw` | Ningún servicio de este proyecto publica puertos en producción (el base no los lleva y CI lo comprueba); el compose de desarrollo no se despliega |
| Un solo VPS (sin alta disponibilidad) | Aceptado a esta escala; backups externos + runbook permiten reconstruir |
| Dockerfile ignora `Directory.*.props` | Copiarlos antes del `restore` (3.6) |

## Decisiones pendientes (afectan a las fases 4, 6 y 7)

1. ~~Proveedor, SO y acceso del VPS~~ — resuelto: Contabo compartido, usuario `maomauro` (fase 6).
2. ~~Dominio y DNS~~ — resuelto: `sitiosapps.com` en Cloudflare. nombres confirmados: subdominio y carpeta `gdb-crud`, alias `gdb-crud-web`.
3. Si Basic Auth basta como autenticación inicial (4.2) o se quiere login propio ya.
4. Barreras de rama (2.3): se mantiene la opción A (disciplina + hook) o se pasa a B (repo público) o C (GitHub Pro). La cuenta es Free.

5. ~~Aprobar el [ADR 0011](adr/0011-despliegue-detras-del-nginx-compartido.md)~~ — aprobado el 2026-10-10.
6. Ventana para recrear `portalcv-nginx-prod` y fusionar el PR en `Curriculum-Vitae-Web` (6.6).
7. Política de reinicio única para los tres proyectos (hoy Colfe `always`, PortalCV y este `unless-stopped`): fijar un estándar.

Resuelto: el despliegue ocurre al fusionar `develop` en `main` (D7).
