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
| Autenticación mínima | Basic Auth en Caddy (un usuario y una clave compartidos); login propio es mejora futura |

## Resumen de fases

| # | Fase | Resultado | Estado | Esfuerzo |
|---|---|---|---|---|
| 0 | Herramientas locales | Máquina lista | ✅ | — |
| 1 | Repositorio local | Git inicializado, convenciones, commit base | ✅ | — |
| 2 | Repositorio remoto | GitHub con `main` y `develop`, ajustes de merge; barreras según 2.3 | ✅ | 1 h |
| 3 | Ambiente local y primer build | Compose de desarrollo, build real, scripts de verificación | ✅ | 3–4 h |
| 4 | Remediación del diagnóstico | Hallazgos corregidos con pruebas | 🔄 | 2–3 días |
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
- ✅ `.github/dependabot.yml` (issue #30): `nuget` (con grupos `corewcf` y `pruebas`), `docker` (una entrada por carpeta con Dockerfile), `docker-compose` y `github-actions`; semanal los lunes, `target-branch: develop`, prefijo `build(deps)` y sin saltos de versión mayor de .NET ni de PostgreSQL.
- ✅ Seguridad (activado el 2026-10-09 por API): Dependabot alerts y actualizaciones automáticas de seguridad (0 alertas abiertas). Secret scanning y push protection no están disponibles en repos privados con plan Free.
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
- Pendiente para el CI (fase 5): ejecutar `shellcheck db/migrate.sh` y `scripts/probar-migrador.ps1` (requiere Docker; en Linux habría que portarlo a un script `sh`/job de GitHub Actions).
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
| `GrupoDigitalBank.Presentacion` | 35,3 % | 87 %; lo no cubierto es el camino feliz de `UsuarioClient` (necesita un servidor CoreWCF real: hallazgo B2, #19) |

`coverlet.runsettings` excluye `Program.cs` (arranque), los `*.g.cs` generados por fuentes (invocador de operaciones de CoreWCF y `LoggerMessage`) y el código de las propias pruebas. El script devuelve código 1 si Negocios baja del umbral (`-Umbral`, por omisión 80; `-SinUmbral` solo informa). `ReportGenerator` va fijado en `.config/dotnet-tools.json` (5.5.11). El CI de la fase 5 debe ejecutar este script.

**Las pruebas detectan errores reales (pruebas de mutación manuales):** se aplicaron 21 mutaciones al código de producción, una por una —`>` por `>=` en cada límite, filtrar el detalle interno de un error de BD al cliente, quitar un `Trim` (validador, repositorio y modelo), invertir una condición, dejar de registrar un error, no persistir las claves de Data Protection, volver a un `onsubmit` en línea (H1)— y **las 21 fueron detectadas**. El código quedó intacto (`git status` limpio). El 100 % de líneas por sí solo no demostraría nada; esto sí.

Notas de implementación:

- Los tres proyectos llevan `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Sin él, `CoreWCF.Primitives` arrastra `System.Security.Cryptography.Xml` 8.0.x con vulnerabilidad alta (NU1903) que en la aplicación queda podada por el framework y en un proyecto de prueba plano no.
- `PostgresFixture` aplica los scripts `db/NNN_*.sql` en orden con `psql` dentro del contenedor (igual que el migrador, con `app_user` y `app_pass`) y expone dos conexiones: `DataSource` (rol de aplicación, el que usa Negocios) y `AdminDataSource` (propietario, solo para preparar y verificar permisos). Las pruebas de la clase comparten la base: cada una usa nombres únicos y no depende del orden ni del conteo.
- Las pruebas de integración llevan `[Trait("Categoria", "Integracion")]`; `dotnet test --filter "Categoria!=Integracion"` las omite si no hay Docker.
- `UsuarioClientTests` usa un servidor TCP local que **acepta y cierra la conexión al instante** para simular un servicio caído: contra un puerto cerrado cada llamada tardaba ≈2 s en Windows (14 s en total) y ahora toda la clase tarda milisegundos, sin depender de que un puerto concreto esté libre.
- Los límites de fecha usan `DateTime.Today`: siguen sujetos a la zona horaria del equipo (hallazgo B6, #17). Las pruebas de base usan +2 días para la fecha futura y evitan el borde.
- **Hallazgo nuevo (issue #41):** la base acepta un nombre vacío o de solo espacios; solo existen `CHECK` de sexo y fecha. Se comprobó con una transacción revertida. No se congeló en una prueba como comportamiento deseado.

Pendiente para otros issues: camino feliz y fault reales del cliente SOAP contra CoreWCF y la comparación de contratos (B2, #19); `TimeProvider` (B6, #17).

### 4.8 H8 y baja prioridad

| Ítem | Estado |
|---|---|
| H8 `docs/` vacío | Pendiente (#16): `docs/arquitectura.md` (C4 en Mermaid), `docs/adr/0001-corewcf.md`, `0002-sin-orm.md`, `0003-contrato-soap-duplicado.md` (B2), `0004-autenticacion.md`, `0005-proteccion-de-rama.md` (si aplica 2.3). `.dockerignore` sigue excluyendo `docs/` |
| B1 versiones flotantes | ✅ Hecho en 3.6 |
| B2 contrato duplicado | Pendiente (#19): ADR; prueba con un servidor CoreWCF real (camino feliz y fault del cliente SOAP) y comparación del WSDL de Negocios con `ContratoServicio.cs` |
| B3 healthchecks | ✅ Ver abajo |
| B4 cabeceras y CSP | ✅ Ver abajo |
| B5 contraseña en `appsettings.json` | ✅ Ver abajo |
| B6 zona horaria | Pendiente (#17): `TimeProvider` inyectable con `America/Bogota`; en la BD, `(now() AT TIME ZONE 'America/Bogota')::date`; pruebas con `FakeTimeProvider` |
| B7 paginación | Diferido (#18) |
| B8 WSDL solo en desarrollo | ✅ Ver abajo |
| Nuevo (#41) | La BD acepta nombres vacíos; falta un `CHECK` en un script `003` |

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
AUTH_HASH='<hash generado con scripts/generar-hash-auth.ps1, entre comillas simples>'
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
