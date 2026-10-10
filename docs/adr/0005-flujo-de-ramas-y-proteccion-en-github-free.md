# ADR 0005: Flujo feature → develop → main y protección de ramas en GitHub Free

- **Estado:** Aceptada
- **Fecha:** 2026-10-10 (decisión D7 del 2026-10-09)
- **Referencias:** [guía de Git](../git-guia-tecnica.md), [ciclo de vida](../ciclo-de-vida-desarrollo.md), plan de trabajo 2.3

## Contexto

Quien mantiene el proyecto trabaja con el flujo **feature → develop → main**, y `main` despliega a producción. El repositorio es **privado en una cuenta GitHub Free**. Se verificó con la API (2026-10-09) que **proteger una rama devuelve `403: Upgrade to GitHub Pro or make this repository public`**. Según la documentación del plan de GitHub, tampoco estarían disponibles los *rulesets* ni los revisores obligatorios de un *environment* en repositorios privados Free (esto último **no se probó**). En la práctica GitHub **no puede impedir** un push directo a `main` ni exigir que el CI pase antes de fusionar.

## Decisión

1. **Ramas.** `main` es producción y `develop` es integración y rama por defecto. Las ramas de trabajo (`feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/`) salen de `develop`, vuelven por Pull Request con **squash**, y el *release* `develop → main` se fusiona con **merge commit** (nunca squash, para que no diverjan). Los hotfix salen de `main` y se sincronizan de vuelta a `develop`. El despliegue ocurre al fusionar en `main`.
2. **El borrado automático de ramas queda desactivado**, porque borraría `develop` al fusionarla a `main`; las ramas de trabajo se borran con `gh pr merge --squash --delete-branch`.
3. **Las reglas se sostienen con disciplina y un hook local** (`.githooks/pre-push`): rechaza el push directo a `main` y `develop` y ejecuta una verificación rápida. El CI y SonarCloud se ven en cada PR pero **no bloquean** el botón de fusionar.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Trabajo basado en tronco (solo `main`) | No es el flujo que se quiere operar, y `develop` permite acumular cambios probados antes de liberar |
| Hacer el repositorio público | Habilitaría la protección, pero publica el código; descartado mientras no sea una decisión deliberada |
| GitHub Pro | Protección completa, con costo mensual; se puede adoptar más adelante |

## Consecuencias

- ➕ Historial lineal en `develop` y liberaciones explícitas; el hook atrapa el error más probable (empujar a la rama equivocada).
- ➖ **La protección no la impone el servidor:** un `--no-verify`, un push desde otro equipo o un clic en «Merge» con el CI en rojo la evitan. Es disciplina, no un control.
- ➖ Es probable que el *environment* `production` no pueda exigir revisor ni restringirse a `main` (según la documentación del plan; sin probar): el workflow de despliegue debe comprobar `github.ref` por sí mismo.
- ➖ Fusionar un PR con el método equivocado (merge commit en vez de squash) alarga el historial sin dañarlo; ha ocurrido.
- 🔁 Pasar a GitHub Pro (o a un repositorio público) si se suma otra persona al equipo o si el despliegue empieza a manejar datos reales, y registrarlo en un ADR nuevo.
