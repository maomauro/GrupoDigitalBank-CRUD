# Guía técnica de Git: ramas locales y remotas

Aplica a GrupoDigitalBank-CRUD. Remoto: GitHub (`origin`). Producción: rama `main`.

## 1. Conceptos clave

| Concepto | Qué es | Dónde vive |
|---|---|---|
| **Rama local** (`feat/x`) | Puntero a un commit en tu copia del repo | Tu equipo (`.git/refs/heads`) |
| **Rama remota** (`origin/feat/x`) | Copia de solo lectura de lo que hay en GitHub, actualizada en `fetch` | Tu equipo (`.git/refs/remotes/origin`) |
| **Rama en el remoto** (`feat/x` en GitHub) | La rama real del servidor | GitHub |
| **Tracking / upstream** | Vínculo entre una rama local y su rama remota | Configuración de la rama local |
| **HEAD** | La rama o commit en el que estás trabajando | Tu equipo |

Regla mental: **`origin/xxx` es la última foto que tu equipo tomó de GitHub**; no cambia hasta que hagas `fetch`, `pull` o `push`.

```
  GitHub (origin)            Tu equipo
  ┌──────────────┐  fetch   ┌────────────────┐  merge/rebase  ┌──────────────┐
  │ main         │ ───────▶ │ origin/main    │ ─────────────▶ │ main (local) │
  │ feat/x       │ ◀─────── │ origin/feat/x  │ ◀───────────── │ feat/x       │
  └──────────────┘   push   └────────────────┘     commit     └──────────────┘
```

## 2. Modelo de ramas del proyecto

Flujo basado en tronco (*trunk-based*) con ramas cortas. No hay `develop`: un solo despliegue, un solo VPS.

| Rama | Local | Remota | Vida | Protegida | Propósito |
|---|---|---|---|---|---|
| `main` | Sí | `origin/main` | Permanente | Sí | Producción. Todo lo que entra se despliega |
| `feat/<tema>` | Sí | Sí (al hacer push) | Horas o días | No | Funcionalidad nueva |
| `fix/<tema>` | Sí | Sí | Horas o días | No | Corrección de errores y hallazgos |
| `chore/<tema>` | Sí | Sí | Corta | No | Mantenimiento, herramientas, dependencias |
| `docs/<tema>` | Sí | Sí | Corta | No | Solo documentación |
| `ci/<tema>` | Sí | Sí | Corta | No | Workflows y despliegue |
| `test/<tema>` | Sí | Sí | Corta | No | Solo pruebas |
| `hotfix/<tema>` | Sí | Sí | Muy corta | No | Corrección urgente desde `main` |

Reglas:

1. `main` nunca recibe commits directos: solo por Pull Request con checks verdes.
2. Una rama = un propósito = un PR. Vida máxima recomendada: 3 días.
3. Al fusionar, **se borran la rama remota y la local**.
4. Los nombres van en minúscula, con guiones: `fix/h1-xss-eliminar`, `chore/base-repositorio`.
5. Versiones: tags anotados `vMAJOR.MINOR.PATCH` sobre `main`.

### Ramas previstas por el plan de trabajo

| Rama | Cierra | Fase del plan |
|---|---|---|
| `chore/base-repositorio` | H3, B1 | 1 |
| `fix/h1-xss-eliminar` | H1 | 3.1 |
| `feat/autenticacion-basica` | H2 | 3.2 |
| `feat/rol-aplicacion-bd` | H4 | 3.4 |
| `feat/migraciones` | H5 | 3.5 |
| `fix/data-protection-claves` | H6 | 3.6 |
| `test/base-pruebas` | H7 | 3.7 |
| `docs/arquitectura-adr` | H8 | 3.8 |
| `chore/hardening-compose-caddy` | B3, B4, B5, B8 | 3.8 |
| `fix/zona-horaria` | B6 | 3.8 |
| `ci/integracion-continua` | CI | 5 |
| `ci/despliegue-vps` | CD | 7 |

## 3. Configuración inicial (una sola vez)

```powershell
git config --global user.name  "maomauro"           # ya configurado
git config --global user.email "maomauro.c@gmail.com" # ya configurado
git config --global init.defaultBranch main
git config --global core.autocrlf input
git config --global pull.rebase true          # pull = fetch + rebase (historial lineal)
git config --global fetch.prune true          # elimina referencias remotas borradas
git config --global push.autoSetupRemote true # el primer push crea el vínculo upstream
git config --global rebase.autoStash true
git config --global rerere.enabled true       # recuerda resoluciones de conflictos
```

Conectar con GitHub:

```powershell
git remote add origin https://github.com/maomauro/GrupoDigitalBank-CRUD.git
git remote -v
gh auth status                                # verifica la sesión (HTTPS, decisión D1)
```

`gh repo create ... --source . --remote origin` (fase 4 del plan) ya añade el remoto por sí mismo. SSH queda reservado para el VPS.

## 4. Ver el estado de ramas locales y remotas

```powershell
git branch                      # solo locales
git branch -r                   # solo remotas (origin/*)
git branch -a                   # todas
git branch -vv                  # locales con su upstream y adelanto/atraso: [origin/main: ahead 1, behind 2]
git remote show origin          # qué ramas hay en GitHub, cuáles están rastreadas y cuáles obsoletas
git log --oneline --graph --decorate --all -20
git status -sb                  # estado corto con el upstream
```

Cómo leer `git branch -vv`:

```
* feat/x   a1b2c3d [origin/feat/x: ahead 2] feat: agrega validador
  main     9f8e7d6 [origin/main] chore: base
  vieja    1234567 [origin/vieja: gone] fix: ...      ← la remota ya se borró: borrar la local
```

## 5. Flujo diario de trabajo

### 5.1 Empezar una tarea

```powershell
git switch main
git pull                                   # actualiza main local desde origin/main
git switch -c fix/h1-xss-eliminar          # rama local nueva desde main
```

### 5.2 Trabajar y confirmar

```powershell
git status
git add src/...                            # añadir archivos concretos; evitar "git add ." sin revisar
git diff --staged                          # revisar antes de confirmar
git commit -m "fix(web): evita XSS en confirmación de eliminar"
```

Formato (Conventional Commits, en español, imperativo, ≤ 72 caracteres en la primera línea):

```
<tipo>(<alcance opcional>): <descripción>

<cuerpo opcional: qué y por qué>

Refs: H1
```

Tipos: `feat`, `fix`, `docs`, `test`, `chore`, `ci`, `refactor`, `perf`, `build`. Cambio incompatible: `feat!:` o pie `BREAKING CHANGE:`.

### 5.3 Publicar la rama (local → remoto)

```powershell
git push                                   # primera vez: crea origin/fix/h1-xss-eliminar y fija el upstream
# si push.autoSetupRemote no está activo:
git push -u origin fix/h1-xss-eliminar
gh pr create --fill --base main            # abre el Pull Request
```

### 5.4 Mantener la rama al día con `main`

```powershell
git fetch origin
git rebase origin/main                     # reaplica tus commits sobre main actualizado
git push --force-with-lease                # SOLO en tu rama de trabajo, tras un rebase
```

`--force-with-lease` falla si alguien más empujó a esa rama; nunca usar `--force` a secas, y **nunca** forzar `main`.

### 5.5 Fusionar y limpiar

El PR se fusiona en GitHub con **Squash and merge** (un commit por PR en `main`, historial lineal; el título del PR sigue Conventional Commits). Luego:

```powershell
git switch main
git pull
git branch -d fix/h1-xss-eliminar          # borra la local
git push origin --delete fix/h1-xss-eliminar   # solo si GitHub no la borró (activar "Automatically delete head branches")
git fetch --prune                          # limpia origin/* obsoletas
```

## 6. Recetas por situación

| Necesidad | Comando |
|---|---|
| Traer una rama remota que no tengo | `git fetch origin` → `git switch feat/x` (crea la local con upstream automático) |
| Crear local desde una remota con otro nombre | `git switch -c mi-rama origin/feat/x` |
| Vincular una local existente a su remota | `git branch --set-upstream-to=origin/feat/x` |
| Renombrar rama local y remota | `git branch -m viejo nuevo` → `git push -u origin nuevo` → `git push origin --delete viejo` |
| Borrar locales ya fusionadas | `git branch --merged main` y `git branch -d <rama>` |
| Ver qué hay en remoto que no tengo | `git fetch` → `git log HEAD..origin/main --oneline` |
| Ver qué tengo que no está en remoto | `git log origin/main..HEAD --oneline` |
| Guardar trabajo a medias | `git stash push -m "wip"` / `git stash pop` |
| Deshacer el último commit sin perder cambios | `git reset --soft HEAD~1` |
| Deshacer un commit ya publicado | `git revert <sha>` (crea un commit nuevo; no reescribe historia) |
| Descartar cambios de un archivo | `git restore <archivo>` |
| Recuperar algo "perdido" | `git reflog` → `git switch -c rescate <sha>` |
| Etiquetar una versión | `git tag -a v1.0.0 -m "Primera versión" ; git push origin v1.0.0` |
| Comparar rama con main | `git diff origin/main...HEAD` |

## 7. Conflictos

```powershell
git fetch origin
git rebase origin/main
# Git se detiene en el archivo en conflicto:
#   editar, eliminar marcadores <<<<<<< ======= >>>>>>>
git add <archivo>
git rebase --continue                      # o: git rebase --abort para volver al estado previo
```

Para scripts SQL numerados: si dos ramas crean el mismo `00N_…sql`, la segunda en fusionarse **renumera** su archivo. Nunca editar un script ya fusionado.

## 8. Hotfix en producción

```powershell
git switch main && git pull
git switch -c hotfix/caida-negocios
# corregir, probar, commit
git push
gh pr create --base main --fill --label urgente
# tras el merge, el CD despliega; luego crear tag de parche
git tag -a v1.0.1 -m "hotfix: ..." ; git push origin v1.0.1
```

Para volver a una versión anterior en producción: ver "Rollback" en la fase 7 del plan (re-despliegue de un tag/SHA previo). Evitar `git reset --hard` sobre `main` compartida; usar `git revert`.

## 9. Protección de `main` en GitHub (configuración)

- Requiere Pull Request antes de fusionar.
- Checks obligatorios: `ci` y `docker`.
- Rama actualizada antes de fusionar.
- Historial lineal (solo squash o rebase).
- Sin force-push ni borrado.
- Aplicar también a administradores.
- *Automatically delete head branches* activado.
- Etiquetas `v*` protegidas contra borrado.

## 10. Qué nunca va al repositorio

`.env`, `*.pem`, `*.key`, llaves SSH, contraseñas, `bin/`, `obj/`, `TestResults/`, volcados de BD con datos reales. Si un secreto se sube por error: **rotarlo de inmediato** (borrar el commit no basta, GitHub y los clones ya lo tienen) y luego limpiar el historial.

## 11. Referencia rápida

```powershell
git switch main; git pull                         # empezar el día
git switch -c feat/tema                           # nueva rama
git add <archivos>; git commit -m "tipo: mensaje" # confirmar
git push; gh pr create --fill                     # publicar y abrir PR
git fetch --prune; git branch -vv                 # estado de local vs remoto
git switch main; git pull; git branch -d feat/tema  # limpiar tras el merge
```
