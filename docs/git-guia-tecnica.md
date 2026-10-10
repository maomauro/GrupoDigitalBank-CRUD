# Guía técnica de Git: ramas locales y remotas

Aplica a GrupoDigitalBank-CRUD. Remoto: GitHub (`origin`), cuenta gratuita. Flujo: **feature → develop → main → producción**.

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
  GitHub (origin)              Tu equipo
  ┌────────────────┐  fetch   ┌──────────────────┐  merge/rebase  ┌────────────────┐
  │ develop        │ ───────▶ │ origin/develop   │ ─────────────▶ │ develop (local)│
  │ feat/x         │ ◀─────── │ origin/feat/x    │ ◀───────────── │ feat/x         │
  └────────────────┘   push   └──────────────────┘     commit     └────────────────┘
```

## 2. Modelo de ramas del proyecto

```
 feat/x ──PR (squash)──▶ develop ──PR (merge commit)──▶ main ──CD──▶ Producción (VPS)
 fix/y  ──PR (squash)──▶    ▲                            │
                            └──── hotfix/z ──PR──▶ main ─┘   (luego main → develop)
```

| Rama | Se crea desde | Va a (PR) | Vida | Propósito |
|---|---|---|---|---|
| `main` | — | — | Permanente | **Producción.** Todo lo que entra se despliega |
| `develop` | `main` | `main` (release) | Permanente | Integración. Aquí se juntan y prueban los cambios antes de liberar |
| `feat/<tema>` | `develop` | `develop` | Horas o días | Funcionalidad nueva |
| `fix/<tema>` | `develop` | `develop` | Horas o días | Corrección de errores y hallazgos |
| `chore/<tema>` | `develop` | `develop` | Corta | Mantenimiento, herramientas, dependencias |
| `docs/<tema>` | `develop` | `develop` | Corta | Solo documentación |
| `ci/<tema>` | `develop` | `develop` | Corta | Workflows y despliegue |
| `test/<tema>` | `develop` | `develop` | Corta | Solo pruebas |
| `hotfix/<tema>` | `main` | `main`, y después `main` → `develop` | Muy corta | Corrección urgente en producción |

Los prefijos `feat/`, `fix/`, `chore/`, `docs/`, `ci/` y `test/` son todos "ramas de feature": indican el tipo de cambio y salen y vuelven a `develop`.

Reglas:

1. **Nadie hace commit directo en `main` ni en `develop`.** Todo entra por Pull Request.
2. Una rama = un propósito = un PR. Vida máxima recomendada: 3 días.
3. **Feature → `develop`: Squash and merge** (un commit por cambio, historial limpio; el título del PR sigue Conventional Commits).
4. **`develop` → `main`: Create a merge commit** (nunca squash). Con squash, `develop` y `main` divergen y cada release arrastra conflictos fantasma.
5. **`develop` nunca se borra.** Por eso la opción de GitHub *Automatically delete head branches* queda **desactivada** (borraría `develop` al fusionarla a `main`). Las ramas de feature se borran con `gh pr merge --squash --delete-branch` o a mano.
6. Los nombres van en minúscula, con guiones: `fix/h1-xss-eliminar`, `chore/ambiente-desarrollo`.
7. Un despliegue = un merge a `main`. Cada release lleva tag anotado `vMAJOR.MINOR.PATCH` sobre `main`.
8. `main` siempre debe ser desplegable; `develop` siempre debe compilar y pasar el CI.

Rama por defecto en GitHub: **`develop`**. Así los PR apuntan a `develop` por omisión, `Closes #n` cierra el issue al fusionar en `develop` y Dependabot abre sus PR contra `develop`.

### Ramas previstas por el plan de trabajo

Todas salen de `develop` y vuelven a `develop`.

| Rama | Cierra | Fase del plan |
|---|---|---|
| `main` (commit base `ce8dc86`) | H3 | 1 ✅ |
| `develop` | Rama de integración (creada desde `main`) | 2 |
| `docs/ajuste-plan-trabajo` | Plan y guías coherentes con la implementación y con este flujo | 2.4 |
| `chore/ambiente-desarrollo` | L6–L11, B1 (compose de desarrollo, primer build, hook, versiones fijas) | 3 |
| `fix/h1-xss-eliminar` | H1 | 4.1 |
| `feat/migraciones` | H5 (antes que H4) | 4.4 |
| `feat/rol-aplicacion-bd` | H4 | 4.5 |
| `feat/autenticacion-basica` | H2 | 4.2 |
| `fix/data-protection-claves` | H6 | 4.6 |
| `test/base-pruebas` | H7 | 4.7 |
| `docs/arquitectura-adr` | H8 | 4.8 |
| `chore/hardening-compose-caddy` | B3, B4, B5, B8 | 4.8 |
| `fix/zona-horaria` | B6 | 4.8 |
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

Conectar con GitHub (ya hecho por `gh repo create`):

```powershell
git remote add origin https://github.com/maomauro/GrupoDigitalBank-CRUD.git
git remote -v
gh auth status                                # verifica la sesión (HTTPS, decisión D1)
```

Crear `develop` (una sola vez) y dejarla como rama por defecto:

```powershell
git switch main; git pull
git switch -c develop
git push -u origin develop
gh repo edit maomauro/GrupoDigitalBank-CRUD --default-branch develop
```

## 4. Ver el estado de ramas locales y remotas

```powershell
git branch                      # solo locales
git branch -r                   # solo remotas (origin/*)
git branch -a                   # todas
git branch -vv                  # locales con su upstream y adelanto/atraso: [origin/develop: ahead 1, behind 2]
git remote show origin          # qué ramas hay en GitHub, cuáles están rastreadas y cuáles obsoletas
git log --oneline --graph --decorate --all -20
git status -sb                  # estado corto con el upstream
git log origin/main..origin/develop --oneline   # lo que está en develop y aún no se liberó
```

Cómo leer `git branch -vv`:

```
* feat/x   a1b2c3d [origin/feat/x: ahead 2] feat: agrega validador
  develop  9f8e7d6 [origin/develop] chore: base
  main     ce8dc86 [origin/main] chore: estado inicial
  vieja    1234567 [origin/vieja: gone] fix: ...      ← la remota ya se borró: borrar la local
```

## 5. Flujo diario de trabajo (feature → develop)

### 5.1 Empezar una tarea

```powershell
git switch develop
git pull                                   # actualiza develop local desde origin/develop
git switch -c fix/h1-xss-eliminar          # rama local nueva desde develop
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

### 5.3 Publicar la rama y abrir el PR (local → remoto)

```powershell
git push                                   # primera vez: crea origin/fix/h1-xss-eliminar y fija el upstream
gh pr create --fill --base develop         # abre el Pull Request hacia develop
```

### 5.4 Mantener la rama al día con `develop`

```powershell
git fetch origin
git rebase origin/develop                  # reaplica tus commits sobre develop actualizado
git push --force-with-lease                # SOLO en tu rama de trabajo, tras un rebase
```

`--force-with-lease` falla si alguien más empujó a esa rama; nunca usar `--force` a secas, y **nunca** forzar `develop` ni `main`.

### 5.5 Fusionar y limpiar

Con el CI en verde y el checklist del PR completo:

```powershell
gh pr merge --squash --delete-branch       # feature → develop: un commit, y borra la rama remota
git switch develop
git pull
git branch -d fix/h1-xss-eliminar          # borra la local
git fetch --prune                          # limpia origin/* obsoletas
```

## 6. Liberar a producción (develop → main)

Cuando `develop` tiene un conjunto de cambios probado (modo A en Docker, CI verde) y listo para publicar:

```powershell
git fetch origin
git log origin/main..origin/develop --oneline        # revisar qué se libera
gh pr create --base main --head develop --title "release: v1.1.0" --body "Cambios: ..."
```

1. Revisar el PR: CI verde y checklist de despliegue (backup al día, migraciones revisadas).
2. Fusionar con **Create a merge commit** (no squash, no rebase): `gh pr merge --merge` **sin** `--delete-branch`.
3. El push a `main` dispara el despliegue (fase 7). Verificar el smoke test.
4. Etiquetar el release:

```powershell
git switch main; git pull
git tag -a v1.1.0 -m "Release 1.1.0"
git push origin v1.1.0
gh release create v1.1.0 --generate-notes
```

5. `develop` ya contiene todo lo de `main` (se fusionó desde ella). Si `main` recibió el merge commit, traerlo a `develop`: `git switch develop; git pull; git merge --ff-only origin/main` (si no es posible fast-forward, abrir un PR `main` → `develop`).

## 7. Recetas por situación

| Necesidad | Comando |
|---|---|
| Traer una rama remota que no tengo | `git fetch origin` → `git switch feat/x` (crea la local con upstream automático) |
| Crear local desde una remota con otro nombre | `git switch -c mi-rama origin/feat/x` |
| Vincular una local existente a su remota | `git branch --set-upstream-to=origin/feat/x` |
| Renombrar rama local y remota | `git branch -m viejo nuevo` → `git push -u origin nuevo` → `git push origin --delete viejo` |
| Borrar locales ya fusionadas | `git branch --merged develop` y `git branch -d <rama>` (excluir `main` y `develop`) |
| Ver qué hay en remoto que no tengo | `git fetch` → `git log HEAD..origin/develop --oneline` |
| Ver qué tengo que no está en remoto | `git log origin/develop..HEAD --oneline` |
| Guardar trabajo a medias | `git stash push -m "wip"` / `git stash pop` |
| Deshacer el último commit sin perder cambios | `git reset --soft HEAD~1` |
| Deshacer un commit ya publicado | `git revert <sha>` (crea un commit nuevo; no reescribe historia) |
| Descartar cambios de un archivo | `git restore <archivo>` |
| Recuperar algo "perdido" | `git reflog` → `git switch -c rescate <sha>` |
| Comparar rama con develop | `git diff origin/develop...HEAD` |

## 8. Conflictos

```powershell
git fetch origin
git rebase origin/develop
# Git se detiene en el archivo en conflicto:
#   editar, eliminar marcadores <<<<<<< ======= >>>>>>>
git add <archivo>
git rebase --continue                      # o: git rebase --abort para volver al estado previo
```

Para scripts SQL numerados: si dos ramas crean el mismo `00N_…sql`, la segunda en fusionarse **renumera** su archivo. Nunca editar un script ya fusionado.

## 9. Hotfix en producción

Sale de `main` (lo que está en producción), no de `develop`, que puede tener cambios sin liberar.

```powershell
git switch main; git pull
git switch -c hotfix/caida-negocios
# corregir, probar, commit
git push
gh pr create --base main --fill --label urgente
gh pr merge --squash --delete-branch        # el CD despliega
git switch main; git pull
git tag -a v1.0.1 -m "hotfix: ..."; git push origin v1.0.1
```

**Sincronizar siempre con `develop`** para no perder la corrección:

```powershell
gh pr create --base develop --head main --title "chore: sincroniza hotfix v1.0.1 en develop"
# fusionar con "Create a merge commit"; no borrar main
```

Volver a una versión anterior en producción: ver "Rollback" en la fase 7 del plan (re-despliegue de un tag/SHA previo). Evitar `git reset --hard` sobre ramas compartidas; usar `git revert`.

## 10. Reglas de protección (cuenta gratuita)

GitHub Free **no permite proteger ramas ni crear rulesets en repos privados** (verificado: `403 Upgrade to GitHub Pro or make this repository public`). Las reglas no las impone el servidor y se sostienen así:

| Mecanismo | Qué hace | Límite |
|---|---|---|
| Hook `.githooks/pre-push` (fase 3.5) | Rechaza `git push` directo a `main` y `develop` (permite solo `--force-with-lease` en ramas propias) | Se salta con `--no-verify` o desde otro equipo |
| CI en cada PR (fase 5) | Falla visible en el PR si hay error de build, formato, pruebas o vulnerabilidades | No bloquea el botón de fusionar: hay que respetarlo |
| Ajustes de merge del repo | Squash y merge commit habilitados; rebase deshabilitado; borrado automático de ramas **desactivado** | La elección squash/merge por PR es manual |
| Checklist del PR | Recuerda tipo de fusión, pruebas y secretos | Es disciplina |
| Rama por defecto `develop` | PR hacia `develop` por omisión | — |

Si se pasa a GitHub Pro o a repositorio público (decisión en el plan, 2.3), activar además en `main` y `develop`: PR obligatorio, checks `ci` y `docker` obligatorios, sin force-push ni borrado, historial lineal solo en `develop`, y proteger las etiquetas `v*`. El environment `production` con revisor requerido también depende de ese plan.

## 11. Qué nunca va al repositorio

`.env`, `*.pem`, `*.key`, llaves SSH, contraseñas, `bin/`, `obj/`, `TestResults/`, volcados de BD con datos reales. Si un secreto se sube por error: **rotarlo de inmediato** (borrar el commit no basta, GitHub y los clones ya lo tienen) y luego limpiar el historial.

## 12. Referencia rápida

```powershell
git switch develop; git pull                         # empezar el día
git switch -c feat/tema                              # nueva rama desde develop
git add <archivos>; git commit -m "tipo: mensaje"    # confirmar
git push; gh pr create --fill --base develop         # publicar y abrir PR
gh pr merge --squash --delete-branch                 # fusionar la feature en develop
git fetch --prune; git branch -vv                    # estado de local vs remoto
gh pr create --base main --head develop              # liberar: PR de release (fusionar con merge commit)
```
