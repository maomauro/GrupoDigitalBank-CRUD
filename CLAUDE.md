# CLAUDE.md

Contexto para Claude Code en este repositorio. Mantenerlo corto y actualizado; si algo aquí contradice el código, el código manda y hay que corregir este archivo.

## Qué es

Sistema de gestión de usuarios (nombre, fecha de nacimiento, sexo) con registro, grilla de consulta, modificación y eliminación. Arquitectura por capas en .NET 10, desplegado con Docker Compose + Caddy (HTTPS) en un VPS propio.

## Arquitectura

```
Navegador ─HTTPS─▶ Caddy ─▶ Presentacion (ASP.NET Core MVC)
                                │ SOAP / BasicHttpBinding
                                ▼
                           Negocios (CoreWCF 1.9.x) ─Npgsql─▶ PostgreSQL (fn_usuario_crud)
```

| Capa | Ruta | Regla |
|---|---|---|
| Presentación | `src/GrupoDigitalBank.Presentacion` | Solo habla SOAP con Negocios. **No referenciar el proyecto Negocios** ni acceder a la BD. |
| Negocios | `src/GrupoDigitalBank.Negocios` | Única capa con acceso a la BD. Contratos, validación, repositorio, servicio. |
| Datos | `db/NNN_*.sql` | Scripts numerados. Una vez aplicados **no se editan**; todo cambio es un script nuevo. |

El contrato SOAP está duplicado a mano en `Presentacion/Servicios/ContratoServicio.cs`. Al cambiar `Negocios/Contratos/*`, actualizar también ese archivo.

## Comandos

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1            # format + build + test (igual que CI)
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1 -Rapido    # format whitespace + build (~10 s)
powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1   # chequeo del entorno
git config core.hooksPath .githooks          # una vez por clon: activa el hook pre-push
docker compose logs -f negocios
```

`TreatWarningsAsErrors` está activo: cualquier aviso rompe el build. El hook `pre-push` rechaza el push directo a `main` y `develop` y corre la verificación rápida.

Modos de ejecución local (detalle en el plan, fase 3.3):

- **A, stack completo:** `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build` → `http://localhost:8080`.
- **B, programar y depurar:** `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db`, luego `dotnet watch` en Negocios (`:5081`) y Presentacion (`:5080`). La cadena de conexión va en `dotnet user-secrets`, no en `appsettings.json`.
- El `docker-compose.dev.yml` es solo de desarrollo y nunca se despliega.

## Convenciones

- Idioma: código de dominio, mensajes de usuario y documentación en **español**; identificadores técnicos (clases de framework, claves de configuración) como los define la plataforma.
- Validación en tres niveles: DataAnnotations (Presentación), `UsuarioValidador` (Negocios, fuente de verdad), `CHECK` en la BD. Un cambio de regla debe tocar los tres.
- Errores de negocio viajan como `FaultException<ErrorServicio>`; nunca exponer detalles de excepciones al cliente.
- SQL siempre parametrizado (Npgsql); sin ORM.
- Sin handlers JavaScript inline en las vistas (ver H1 del diagnóstico); usar atributos `data-*` y `wwwroot/js`.
- Versiones de paquetes fijas y centralizadas en `Directory.Packages.props`: los `.csproj` no llevan `Version`. Los Dockerfile copian `global.json` y los `Directory.*.props` antes del `restore`; si se añade otro archivo de ese tipo, copiarlo también.
- Cambios de esquema: *expand/contract*, compatibles hacia atrás durante un despliegue.

## Git

Flujo y ramas: ver [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md). Resumen: **feature → develop → main → producción**.

- `main` = producción (su push despliega); `develop` = integración y rama por defecto. Ninguna recibe commits directos: todo por PR (GitHub Free no permite protegerlas en el servidor, así que es regla propia, apoyada en el hook `pre-push`).
- Ramas de trabajo `feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/` salen de `develop` y vuelven a `develop` con **squash**. Release `develop` → `main` con **merge commit** (nunca squash). Hotfix sale de `main` y luego se sincroniza a `develop`.
- No activar el borrado automático de ramas en GitHub (borraría `develop`); usar `gh pr merge --squash --delete-branch` en las features.
- Conventional Commits en español. **Nunca** force-push a `main` ni a `develop`.
- No hacer commit ni push salvo que el usuario lo pida.

## Seguridad: nunca

- Versionar `.env`, contraseñas, llaves o hashes reales.
- Publicar puertos de `db`, `negocios` o `web` en Compose (solo `proxy` publica 80/443).
- Editar un script SQL ya aplicado.
- Desactivar verificación de host SSH o de certificados para "hacerlo funcionar".

## Despliegue

Plan completo en [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md). Producción: GitHub Actions construye imágenes a GHCR y despliega por SSH al VPS en `/opt/gdb`. El `.env` de producción vive solo en el servidor.

## Documentos de referencia

- [docs/diagnostico/diagnostico-2026-10-09.md](docs/diagnostico/diagnostico-2026-10-09.md): hallazgos H1–H8 y B1–B8.
- [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md): fases y prioridades.
- [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md): uso de Git, ramas locales y remotas.
- [README.md](README.md): ejecución y decisiones técnicas.

## Estado conocido

- Fases 0 y 1 del plan hechas: herramientas verificadas, repositorio local con commit base `ce8dc86`. Fase 2 (remoto en GitHub) en curso.
- El proyecto aún no se ha compilado ni probado con `dotnet` (el diagnóstico fue estático; el SQL sí se probó en PostgreSQL). Es el primer paso de la fase 3.
- No existen todavía `tests/`, CI/CD, autenticación, migraciones automáticas ni `docker-compose.dev.yml`; están planificados en el plan de trabajo. Actualizar esta sección cuando se cierren.
- Decisiones de proceso (D1–D6) en [docs/ciclo-de-vida-desarrollo.md](docs/ciclo-de-vida-desarrollo.md): GitHub por HTTPS con `maomauro`, repo privado, Issues, CI obligatorio, modo B para programar y A antes del PR, hook `pre-push`.
