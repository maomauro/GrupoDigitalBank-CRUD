# Ciclo de vida del desarrollo: ambiente local → GitHub

Estado: **en definición** (aún no se implementa). Este documento fija qué necesitamos y cómo se trabaja, para empezar a implementar sin improvisar. El detalle de Git está en [git-guia-tecnica.md](git-guia-tecnica.md); el plan completo hasta producción, en [plan-de-trabajo.md](plan-de-trabajo.md).

## 1. El ciclo

```
   ┌──────────────────────────────────────────────────────────────────────┐
   │                                                                      │
   ▼                                                                      │
 1 Planificar ─▶ 2 Rama ─▶ 3 Desarrollar ─▶ 4 Verificar ─▶ 5 Commit ─▶ 6 Push + PR
   (issue)       local      (local)          (local)        local         │
                                                                          ▼
 10 Aprender ◀─ 9 Operar ◀─ 8 Desplegar ◀─ 7 Revisar + CI + merge a main
   (nuevo issue)  (prod)     (CD, después)    (GitHub)
```

| Etapa | Dónde | Qué ocurre | Salida (puerta para avanzar) |
|---|---|---|---|
| 1 Planificar | GitHub Issues | Un issue por hallazgo o tarea, con criterio de aceptación | Issue con etiqueta de prioridad (P0–P3) |
| 2 Rama | Local | `git switch -c fix/h1-xss-eliminar` desde `main` actualizada | Rama con nombre según la guía |
| 3 Desarrollar | Local | Código en VS Code; ejecutar la app en Docker (ver §3) | Funciona en mi máquina |
| 4 Verificar | Local | build, format, tests (ver §4) | Todo verde antes de confirmar |
| 5 Commit | Local | Commits pequeños, Conventional Commits | Historial legible |
| 6 Push + PR | Local → GitHub | `git push` y `gh pr create` con plantilla | PR abierto enlazado al issue |
| 7 Revisar + CI | GitHub | Checks automáticos, revisión propia con checklist, squash merge | `main` verde |
| 8 Desplegar | GitHub → VPS | Automático desde `main` (se implementa en fases 6–8 del plan) | Versión en producción |
| 9 Operar | VPS | Backups, monitoreo | Sistema estable |
| 10 Aprender | GitHub Issues | Lo observado vuelve como issues | Backlog actualizado |

Alcance de la primera entrega: **etapas 1 a 7**. Las etapas 8 a 10 se activan cuando el VPS esté listo; el ciclo local no depende de ellas.

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
| OpenSSH y llave personal | Acceso al VPS y a Git por SSH | Instalado, llave existente |
| VS Code + C# Dev Kit, YAML, GitHub Actions | Editor | Instalado |
| Extensiones Docker, EditorConfig, SQLTools | Comodidad | **Pendiente** (opcional) |
| psql local | Consultas a la BD | No requerido (`docker compose exec db psql`) |

### 3.2 Brechas por cerrar (lo que hay que "alistar")

| # | Brecha | Acción | Prioridad |
|---|---|---|---|
| L1 | Rama inicial de Git sin configurar | `git config --global init.defaultBranch main` y el resto de la sección 3 de la guía de Git | P0 |
| L2 | Identidad y protocolo Git | Decisión D1 (abajo) | P0 |
| L3 | La carpeta no es repositorio | `git init` + commit base (fase 1 del plan) | P0 |
| L4 | Faltan archivos de convenciones | `global.json`, `.editorconfig`, `.gitattributes`, `Directory.Build.props`, `Directory.Packages.props`, plantilla de PR, plantilla de issues | P0 |
| L5 | `.gitignore` por revisar y `obj/` generado en el árbol | Revisar y limpiar antes del primer commit | P0 |
| L6 | Compose solo tiene modo producción | Definir `docker-compose.dev.yml` (BD en `127.0.0.1:5432`, sin Caddy, web en `http://localhost:8080`) | P0 |
| L7 | Sin configuración de VS Code | `.vscode/launch.json`, `tasks.json`, `extensions.json` (recomendadas) | P1 |
| L8 | Secretos de desarrollo | `.env` desde `.env.example` + `dotnet user-secrets` para el modo B | P0 |
| L9 | El proyecto nunca se ha compilado | Primer `dotnet build` y `docker compose up --build` y corregir | P0 |
| L10 | No hay proyectos de prueba | Esqueleto en `tests/` (xUnit + Testcontainers) | P1 |
| L11 | Sin automatización de la verificación local | Un script `scripts/verificar.ps1` (build + format + test) y, opcional, hook `pre-push` | P1 |
| L12 | Repositorio remoto | `gh repo create`, protección de `main`, plantillas, Dependabot (fase 4) | P0 |
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
| D4 | Revisión de PR trabajando solo | **CI obligatorio + checklist de §2**; sin exigir aprobación de otra persona |
| D5 | Modo diario de ejecución | **B (híbrido)** para programar; **A (todo en Docker)** antes de abrir el PR |
| D6 | Hook `pre-push` local | **Con hook**, solo `format` + `build`; los tests quedan en CI y bajo demanda |

## 6. Orden de alistamiento (previo a implementar)

1. ~~Cerrar D1 a D6~~ (hecho).
2. L1, L2 → configurar Git.
3. L3, L4, L5, L8 → repositorio local con convenciones y commit base.
4. L6, L9 → ambiente Docker de desarrollo y primer build real.
5. L12 → repositorio remoto con protecciones.
6. L7, L10, L11 → comodidad y pruebas.
7. L13 → CI. A partir de aquí rige el ciclo completo (etapas 1–7).

Fase siguiente (no bloquea): VPS y despliegue, según el plan de trabajo.
