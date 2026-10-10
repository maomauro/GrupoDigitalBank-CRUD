# ADR 0010: Estrategia de verificación: pruebas, cobertura mínima y mutación

- **Estado:** Aceptada
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo H7 (issue [#14](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/14)), `scripts/verificar.ps1`, `scripts/cobertura.ps1`

## Contexto

El proyecto no tenía pruebas. Se quiere sostener «código limpio» y poder cambiar con confianza, y un CI que repita exactamente lo que se ejecuta en local. La cobertura de líneas por sí sola **no demuestra que las pruebas detecten errores**.

## Decisión

1. **Pirámide.** Unitarias para la lógica pura (validador, servicio con el repositorio simulado), **integración con PostgreSQL real** (Testcontainers) para el repositorio y la base, y pruebas de la web con `WebApplicationFactory` y el cliente SOAP simulado. Las pruebas de integración llevan `[Trait("Categoria", "Integracion")]` por si no hay Docker.
2. **Cobertura mínima del 80 % en la lógica de Negocios**, medida con coverlet y ReportGenerator (`scripts/cobertura.ps1`), excluyendo `Program.cs`, el código generado por fuentes y el de las propias pruebas. Hoy: Negocios 100 % de líneas y ramas; Presentación 89 %.
3. **Pruebas de mutación manuales** sobre el código de producción en cada cambio relevante: se aplica un error a propósito (un límite, un `Trim`, un detalle interno filtrado) y se exige que alguna prueba **falle**. 34 aplicadas, 34 detectadas. El script restaura cada archivo desde una copia del texto original, nunca con `git checkout`.
4. **Primero la prueba, y vista en rojo.** Cada corrección de un hallazgo empieza con una prueba que falla por la razón correcta (se descartaron «rojos falsos» causados por errores de la propia prueba).
5. **`verificar.ps1` es el contrato del CI:** formato, compilación con `TreatWarningsAsErrors` y pruebas. Un hook `pre-push` ejecuta la variante rápida ([ADR 0005](0005-flujo-de-ramas-y-proteccion-en-github-free.md)).
6. **Verificación en navegador real** (`probar-navegador.ps1`, opcional) para lo que ninguna prueba de servidor puede ver: CSP y comportamiento de JavaScript.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Solo cobertura de líneas como criterio | Se puede cubrir todo sin comprobar nada; por eso se añade la mutación |
| Una herramienta automática de mutación (Stryker.NET) | Candidata para el futuro; hoy 34 mutaciones dirigidas dan la señal con menos coste |
| Base de datos simulada en memoria | No reproduce `CHECK`, `SECURITY DEFINER` ni permisos: justo lo que se quiere probar |

## Consecuencias

- ➕ Los hallazgos de seguridad y de negocio quedaron cubiertos con una prueba de regresión que se vio fallar antes de corregirlos.
- ➖ Las pruebas de integración necesitan Docker y arrancan un contenedor por clase (≈1 s).
- ➖ La mutación manual no es automática ni escala con el tamaño del proyecto.
- ➖ ~~El CI aún no existe~~ **Resuelto en la fase 5 (#20):** `.github/workflows/ci.yml` ejecuta `verificar.ps1` y `cobertura.ps1` en cada PR, además de lo que solo tiene sentido en un servidor limpio.
- 🔁 Evaluar Stryker.NET en el CI y subir el mínimo de cobertura cuando crezca el código.
