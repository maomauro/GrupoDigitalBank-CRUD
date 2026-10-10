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
| Datos | `db/NNN_*.sql`, `db/migrate.sh` | Scripts numerados que aplica el servicio `migrador` (checksum en `schema_migrations`, una transacción por script, aborta si un script aplicado cambia). Una vez aplicados **no se editan**; todo cambio es un script nuevo. |

El contrato SOAP está duplicado a mano en `Presentacion/Servicios/ContratoServicio.cs`. Al cambiar `Negocios/Contratos/*`, actualizar también ese archivo: `tests/GrupoDigitalBank.Contrato.Tests` (único proyecto que referencia ambas capas) compara los dos contratos por reflexión y llama al servicio real con el cliente real; falla si divergen.

## Comandos

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1            # format + build + test (igual que CI)
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1 -Rapido    # format whitespace + build (~10 s)
powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1   # chequeo del entorno
powershell -ExecutionPolicy Bypass -File scripts\probar-migrador.ps1         # prueba db/migrate.sh en una BD desechable (requiere Docker)
powershell -ExecutionPolicy Bypass -File scripts\cobertura.ps1                          # pruebas + cobertura; exige >= 80 % en Negocios (requiere Docker)
powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1 -ActualizarEnv   # hash de la clave de acceso (AUTH_USUARIO/AUTH_HASH en .env)
powershell -ExecutionPolicy Bypass -File scripts\probar-acceso.ps1 -Usuario admin       # batería de acceso y cabeceras del proxy (stack con --profile con-proxy)
powershell -ExecutionPolicy Bypass -File scripts\probar-navegador.ps1 -Usuario admin    # CSP y clic real de H1 en Chrome/Edge (opcional: Node + Chrome + Docker)
powershell -ExecutionPolicy Bypass -File scripts\verificar-docs.ps1 [-Diagramas]       # enlaces relativos de los .md; con -Diagramas también analiza y dibuja los Mermaid (Node + Chrome)
git config core.hooksPath .githooks          # una vez por clon: activa el hook pre-push
docker compose logs -f negocios
```

Pruebas en `tests/` (xUnit, Shouldly, NSubstitute, Testcontainers). Las de integración arrancan PostgreSQL en Docker: `dotnet test --filter "Categoria!=Integracion"` las omite. Los proyectos de prueba referencian `Microsoft.AspNetCore.App` (evita un NU1903 transitivo de CoreWCF).

`TreatWarningsAsErrors` está activo: cualquier aviso rompe el build. El hook `pre-push` rechaza el push directo a `main` y `develop` y corre la verificación rápida.

Modos de ejecución local (detalle en el plan, fase 3.3):

- **A, stack completo:** `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build` → `http://localhost:8080`.
- **B, programar y depurar:** `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db`, luego `dotnet watch` en Negocios (`:5081`) y Presentacion (`:5080`). La cadena de conexión va en `dotnet user-secrets`, no en `appsettings.json`.
- El `docker-compose.dev.yml` es solo de desarrollo y nunca se despliega.
- VS Code: `.vscode/` trae F5 para Negocios y Presentacion (modo B), tareas de build, pruebas, verificación y Docker, y extensiones recomendadas.

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
- Publicar puertos de `db`, `negocios` o `web` en Compose (solo `proxy` publica 80/443; el `docker-compose.dev.yml` publica la BD solo en `127.0.0.1` y nunca se despliega).
- Conectar la aplicación con `POSTGRES_USER` (administrador): usa siempre `APP_DB_USER`, el rol de mínimo privilegio.
- Guardar la clave de acceso a la web en claro (solo va su hash `AUTH_HASH`, entre comillas simples en el `.env`) ni dejar rutas fuera del Basic Auth salvo `/health`. Al editar el `Caddyfile`, reejecutar `scripts/probar-acceso.ps1`. Las cabeceras de seguridad se importan también desde `handle_errors`: sin eso el 401 sale sin ninguna.
- Poner `style=`, `<style>`, `<script>` o manejadores `on*=` en las vistas, o recursos de otro origen: la CSP del proxy (`script-src 'self'; style-src 'self'`) los bloquea. No usar `asp-validation-summary` (emite `style="display:none"`): usar el parcial `_ResumenErrores`.
- Versionar una cadena de conexión con contraseña en `appsettings*.json`: llega por entorno, `user-secrets` o un archivo local. Negocios no arranca sin ella.
- Usar `DateTime.Today`, `DateTime.Now` o `CURRENT_DATE` para reglas de negocio: «hoy» es la fecha de `America/Bogota` (`TimeProvider` + `Negocios:ZonaHoraria` en C#, y `now() AT TIME ZONE 'America/Bogota'` en SQL, `db/003`). El servidor y la BD están en UTC.
- Restaurar un archivo con `git checkout --` si hay cambios sin confirmar en él (los descarta); al mutar o probar, restaurar desde una copia del texto original.
- Enviar una clave a `caddy hash-password` por la tubería de PowerShell 5.1 (agrega BOM y CRLF y el hash sale de otra cadena): usar `scripts/generar-hash-auth.ps1`.
- Editar un script SQL ya aplicado.
- Desactivar verificación de host SSH o de certificados para "hacerlo funcionar".

## Despliegue

Plan completo en [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md). Producción (diseño propuesto en [ADR 0011](docs/adr/0011-despliegue-detras-del-nginx-compartido.md), aún sin implementar): el VPS es **compartido** con PortalCV y Colfe; Cloudflare y el nginx `portalcv-nginx-prod` (único con 80/443) van delante y enrutan `gdb-crud.sitiosapps.com` a nuestro Caddy por la red compartida. GitHub Actions construye imágenes a GHCR y despliega por SSH con una llave restringida a `/srv/sitiosapps/gdb-crud`. El `.env` de producción vive solo en el servidor. No tocar el nginx, `daemon.json`, el firewall ni SSH del VPS sin aprobación: afectan a los otros proyectos.

## Documentos de referencia

- [docs/diagnostico/diagnostico-2026-10-09.md](docs/diagnostico/diagnostico-2026-10-09.md): hallazgos H1–H8 y B1–B8.
- [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md): fases y prioridades.
- [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md): uso de Git, ramas locales y remotas.
- [docs/arquitectura.md](docs/arquitectura.md): vista C4, flujos, seguridad por capas y despliegue (el de producción es una propuesta hasta la fase 7).
- [docs/adr/README.md](docs/adr/README.md): las 11 decisiones de arquitectura con su porqué. **Un ADR aceptado no se edita**: si la decisión cambia, se escribe uno nuevo que lo reemplaza.
- [README.md](README.md): ejecución y decisiones técnicas.

## Estado conocido

- Fases 0 a 3 del plan hechas, y la fase 4 en curso: **H1, H2, H4, H5, H6, H7, B2 (contrato SOAP), B3/B4/B5/B8 (#15), B6 (zona horaria) y H8 (arquitectura y ADR) resueltos**. Todo está en `develop`; `main` sigue en el commit base y aún no hay nada que desplegar.
- El proyecto compila sin advertencias, corre en modo A y B y tiene 203 pruebas (unitarias, vistas con `WebApplicationFactory`, integración con PostgreSQL real y contrato SOAP real). Cobertura: Negocios 100 %, Presentación 94 % (`scripts/cobertura.ps1`; el CI debe exigir >= 80 % en Negocios). Las 34 mutaciones manuales aplicadas al código y las 7 al contrato (B2) que compilan fueron detectadas.
- Dependabot configurado (`.github/dependabot.yml`), alertas y correcciones automáticas de seguridad activas.
- Siguiente: el CHECK de nombre vacío (#41) y B7 (#18) (B2, contrato SOAP real, hecho); después CI (fase 5), VPS (6) y despliegue (7). El plan de despliegue ya refleja el VPS compartido (nginx + Cloudflare).
- No existe todavía CI/CD; están planificados en el plan de trabajo. Actualizar esta sección cuando se cierren.
- Decisiones de proceso (D1–D7) en [docs/ciclo-de-vida-desarrollo.md](docs/ciclo-de-vida-desarrollo.md): GitHub por HTTPS con `maomauro`, repo privado, Issues, flujo feature → develop → main, modo B para programar y A antes del PR, hook `pre-push`.
