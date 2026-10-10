# Ciclo de vida del desarrollo: ambiente local → GitHub

Estado: definición cerrada; implementación en curso (fase 2 del plan). Este documento fija qué necesitamos y cómo se trabaja. El detalle de Git está en [git-guia-tecnica.md](git-guia-tecnica.md); el plan completo hasta producción, en [plan-de-trabajo.md](plan-de-trabajo.md).

Flujo de ramas (D7): **feature → develop → main → producción**.

## 1. El ciclo

```
   ┌────────────────────────────────────────────────────────────────────────┐
   │                                                                        │
   ▼                                                                        │
 1 Planificar ─▶ 2 Rama ─▶ 3 Desarrollar ─▶ 4 Verificar ─▶ 5 Commit ─▶ 6 Push + PR
   (issue)     (desde        (local)          (local)        local     (hacia develop)
               develop)                                                     │
                                                                            ▼
 10 Aprender ◀─ 9 Operar ◀─ 8 Desplegar ◀─ 7b Liberar ◀─ 7 CI + squash a develop
   (nuevo issue)  (prod)    (al fusionar    (PR develop       (GitHub)
                             en main)        → main)
```

| Etapa | Dónde | Qué ocurre | Salida (puerta para avanzar) |
|---|---|---|---|
| 1 Planificar | GitHub Issues | Un issue por hallazgo o tarea, con criterio de aceptación | Issue con etiqueta de prioridad (P0–P3) |
| 2 Rama | Local | `git switch -c fix/h1-xss-eliminar` desde `develop` actualizada | Rama con nombre según la guía |
| 3 Desarrollar | Local | Código en VS Code; ejecutar la app en Docker (ver §3) | Funciona en mi máquina |
| 4 Verificar | Local | build, format, tests (ver §4) | Todo verde antes de confirmar |
| 5 Commit | Local | Commits pequeños, Conventional Commits | Historial legible |
| 6 Push + PR | Local → GitHub | `git push` y `gh pr create --base develop` con plantilla | PR hacia `develop` enlazado al issue |
| 7 Revisar + CI | GitHub | Checks automáticos, revisión propia con checklist, **squash merge a `develop`** | `develop` verde |
| 7b Liberar | GitHub | PR `develop` → `main` (título `release: vX.Y.Z`), fusionado con **merge commit** | Release en `main` |
| 8 Desplegar | GitHub → VPS | Automático al fusionar en `main` (se implementa en fases 6–8 del plan) | Versión en producción |
| 9 Operar | VPS | Backups, monitoreo | Sistema estable |
| 10 Aprender | GitHub Issues | Lo observado vuelve como issues | Backlog actualizado |

`develop` acumula cambios probados; se libera a `main` cuando hay algo desplegable, no tras cada PR. Los merges a `develop` ejecutan CI pero no despliegan.

Alcance de la primera entrega: **etapas 1 a 7b**. Las etapas 8 a 10 se activan cuando el VPS esté listo; el ciclo local no depende de ellas.

## 2. Definiciones de "listo"

**Listo para empezar** (Definition of Ready): hay issue, criterio de aceptación claro, está priorizado y cabe en ≤ 3 días.

**Terminado** (Definition of Done), todo marcado en el PR:

- [ ] Compila sin advertencias (`-warnaserror`) y `dotnet format` no propone cambios
- [ ] Pruebas nuevas o ajustadas, y todas en verde
- [ ] La app se probó a mano en Docker (flujo afectado)
- [ ] Sin secretos ni archivos generados en el diff
- [ ] Documentación y `CLAUDE.md` actualizados si cambió algo estructural
- [ ] Si toca la BD: script numerado nuevo, nunca se edita uno aplicado
- [ ] Commits con formato Conventional Commits; el PR cierra el issue (`Closes #n`)
- [ ] CI verde en GitHub

## 3. Ambiente local: qué se necesita y qué hay

Principio: **Docker es el entorno de ejecución; el SDK de .NET es la herramienta de trabajo.** Dos modos de ejecutar, según lo que se esté haciendo:

| Modo | Cuándo | Cómo |
|---|---|---|
| **A. Stack completo en Docker** | Probar de punta a punta, validar antes del PR, ensayar el despliegue | `docker compose up -d --build` con `DOMINIO=localhost` |
| **B. Híbrido** | Programar y depurar día a día | Solo `db` en Docker; Negocios y Presentación con F5 en VS Code y `dotnet watch` |

### 3.1 Herramientas (verificado el 2026-10-09 con `scripts/verificar-herramientas.ps1`)

| Herramienta | Rol | Estado |
|---|---|---|
| Git 2.53 | Control de versiones | Instalado |
| .NET SDK 10.0.201 | build, test, format, user-secrets | Instalado |
| Docker Desktop 29.6.2 + Compose v5 | Ejecutar el stack y la BD | Instalado, activo |
| GitHub CLI 2.92 | Repo, PR, issues desde terminal | Instalado, sesión iniciada (`maomauro`) |
| OpenSSH y llave personal | Acceso al VPS (Git va por HTTPS, D1) | Instalado, llave existente |
| VS Code + C# Dev Kit, YAML, GitHub Actions | Editor | Instalado |
| Extensiones Docker, EditorConfig, SQLTools | Comodidad | **Pendiente** (opcional) |
| psql local | Consultas a la BD | No requerido (`docker compose exec db psql`) |

### 3.2 Brechas por cerrar (lo que hay que "alistar")

| # | Brecha | Acción | Prioridad |
|---|---|---|---|
| L1 | Rama inicial de Git sin configurar | `git config --global init.defaultBranch main` y el resto de la sección 3 de la guía de Git | P0 · ✅ |
| L2 | Identidad y protocolo Git | Decisión D1 (abajo) | P0 · ✅ |
| L3 | La carpeta no es repositorio | `git init` + commit base (fase 1 del plan) | P0 · ✅ `ce8dc86` |
| L4 | Faltan archivos de convenciones | `global.json`, `.editorconfig`, `.gitattributes`, `Directory.Build.props`, plantillas de PR e issues, `CODEOWNERS` hechos. `Directory.Packages.props` se crea en la fase 3.6, con las versiones que resuelva el primer build | P0 · ✅ |
| L5 | `.gitignore` por revisar y `obj/` generado en el árbol | Revisado; `obj/` queda ignorado | P0 · ✅ |
| L6 | Compose solo tiene modo producción | `docker-compose.dev.yml` (BD en `127.0.0.1:5432`, sin Caddy, web en `http://localhost:8080`), fase 3.3 | P0 · ✅ |
| L7 | Sin configuración de VS Code | `.vscode/launch.json`, `tasks.json`, `extensions.json`, fase 3.4 | P1 · ✅ |
| L8 | Secretos de desarrollo | `.env` desde `.env.example` + `dotnet user-secrets` para el modo B, fase 3.1 | P0 · ✅ |
| L9 | El proyecto nunca se ha compilado | Primer `dotnet build` y `docker compose up --build` y corregir, fase 3.2 | P0 · ✅ |
| L10 | No hay proyectos de prueba | Esqueleto en `tests/` (xUnit + Testcontainers), fase 3.6 | P1 · ✅ |
| L11 | Sin automatización de la verificación local | `scripts/verificar.ps1` (format + build + test) y hook `pre-push` (D6), fase 3.5 | P1 · ✅ |
| L12 | Repositorio remoto | `gh repo create` ✅, etiquetas ✅, rama `develop` por defecto, ajustes de merge, Dependabot (fase 2). La protección de ramas no existe en plan Free; se sustituye por el hook (L11) | P0 · 🔄 |
| L13 | CI | Workflow `ci.yml` que repite lo de la etapa 4 (fase 5) | P1 |

Cerrar L1 a L12 deja el ciclo de las etapas 1–6 operativo; L13 completa la etapa 7.

## 4. Verificación local (etapa 4), el mismo contrato que CI

Orden fijo, de más rápido a más lento:

1. `dotnet format --verify-no-changes`
2. `dotnet build -warnaserror`
3. `dotnet test` (unitarias, luego integración con PostgreSQL en contenedor)
4. Prueba manual del flujo afectado en modo A
5. `git diff --staged` revisado antes de confirmar

Regla: **lo que CI ejecuta debe poder ejecutarse idéntico en local**. Si CI falla y local pasa, se corrige la diferencia, no se ignora.

## 5. Decisiones (cerradas el 2026-10-09)

| # | Decisión | Resuelto |
|---|---|---|
| D1 | Identidad y protocolo de Git | `maomauro <maomauro.c@gmail.com>` con **HTTPS** (la ya configurada en `gh`). SSH se usa solo para el VPS |
| D2 | Visibilidad del repo | **Privado** |
| D3 | Gestión de tareas | **GitHub Issues**, con una etiqueta por prioridad (P0–P3); el plan sigue siendo el mapa |
| D4 | Revisión de PR trabajando solo | **CI + checklist de §2**; sin exigir aprobación de otra persona. En GitHub Free no se puede imponer en el servidor (ver plan 2.3), se sostiene con el hook y disciplina |
| D5 | Modo diario de ejecución | **B (híbrido)** para programar; **A (todo en Docker)** antes de abrir el PR |
| D6 | Hook `pre-push` local | **Con hook** (`.githooks/pre-push`), solo `format` + `build`, y rechaza el push directo a `main` y `develop`; los tests quedan en CI y bajo demanda (`scripts/verificar.ps1`) |
| D7 | Flujo de ramas (2026-10-09) | **feature → develop → main → producción**. Feature a `develop` con squash; `develop` a `main` con merge commit; el push a `main` despliega; hotfix desde `main` y luego sincronizar a `develop`; `develop` es la rama por defecto |

## 6. Orden de alistamiento (previo a implementar)

1. ✅ Cerrar D1 a D6.
2. ✅ L1, L2 → configurar Git (fase 0).
3. ✅ L3, L4, L5 → repositorio local con convenciones y commit base (fase 1).
4. **L12 → repositorio remoto (fase 2), antes de seguir desarrollando**: desde aquí todo cambio entra por rama y PR.
5. L6, L8, L9 → ambiente Docker de desarrollo y primer build real (fase 3).
6. L7, L10, L11 → VS Code, esqueleto de pruebas, script de verificación y hook (fase 3).
7. L13 → CI (fase 5). Con el CI rige el ciclo completo (etapas 1–7).

Fase siguiente (no bloquea): VPS y despliegue, según el plan de trabajo.
