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
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1            # format + build + test (es lo que ejecuta el trabajo ci)
powershell -ExecutionPolicy Bypass -File scripts\verificar.ps1 -Rapido    # format whitespace + build (~10 s)
powershell -ExecutionPolicy Bypass -File scripts\verificar-herramientas.ps1   # chequeo del entorno
powershell -ExecutionPolicy Bypass -File scripts\probar-migrador.ps1         # prueba db/migrate.sh en una BD desechable (requiere Docker)
powershell -ExecutionPolicy Bypass -File scripts\cobertura.ps1                          # pruebas + cobertura; exige >= 80 % en Negocios (requiere Docker)
powershell -ExecutionPolicy Bypass -File scripts\generar-hash-auth.ps1 -ActualizarEnv   # hash de la clave de acceso (AUTH_USUARIO/AUTH_HASH en .env)
powershell -ExecutionPolicy Bypass -File scripts\probar-acceso.ps1 -Usuario admin       # batería de acceso y cabeceras del proxy (stack con --profile con-proxy)
powershell -ExecutionPolicy Bypass -File scripts\probar-navegador.ps1 -Usuario admin    # CSP y clic real de H1 en Chrome/Edge (opcional: Node + Chrome + Docker)
bash scripts/probar-alertas.sh                                                     # las alertas del servidor (backup viejo, disco lleno, contenedor caido...) con herramientas de mentira
powershell -ExecutionPolicy Bypass -File scripts\probar-backup-externo.ps1          # la copia externa cifrada (age) y su rotación, contra un almacén simulado (Docker)
powershell -ExecutionPolicy Bypass -File scripts\probar-despliegue.ps1               # el mecanismo de despliegue de punta a punta contra un servidor simulado (≈ 7 min; en Linux: bash scripts/probar-despliegue.sh)
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
- La grilla se pagina en el servidor (`ConsultarPagina` y `fn_usuario_pagina`, [ADR 0012](docs/adr/0012-paginacion-en-el-servidor.md)): no volver a traer todos los usuarios para listarlos. Por omisión 5 por página y los controles solo aparecen si hacen falta ([ADR 0017](docs/adr/0017-panel-de-usuarios-edad-y-fecha-de-registro.md)).
- La grilla vive en `/inicio` (panel con sesión) y muestra Edad (la calcula Negocios con la fecha de Bogotá) y Fecha de registro (`db/006`); las acciones son iconos SVG en línea. El nombre se escribe en mayúsculas (campo y Presentación; no es regla de negocio).

## Git

Flujo y ramas: ver [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md). Resumen: **feature → develop → main → producción**.

- `main` = producción (su push despliega); `develop` = integración y rama por defecto. Ninguna recibe commits directos: todo por PR (GitHub Free no permite protegerlas en el servidor, así que es regla propia, apoyada en el hook `pre-push`).
- Ramas de trabajo `feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/` salen de `develop` y vuelven a `develop` con **squash**. Release `develop` → `main` con **merge commit** (nunca squash). Hotfix sale de `main` y luego se sincroniza a `develop`.
- No activar el borrado automático de ramas en GitHub (borraría `develop`); usar `gh pr merge --squash --delete-branch` en las features.
- Conventional Commits en español. **Nunca** force-push a `main` ni a `develop`.
- No hacer commit ni push salvo que el usuario lo pida.

## Seguridad: nunca

- Versionar `.env`, contraseñas, llaves o hashes reales.
- Publicar puertos de **ningún** servicio en el compose base ni en el de producción (el servidor comparte nginx con otros proyectos y Docker salta el firewall; el `proxy` entra por la red compartida y el CI lo comprueba). El `docker-compose.dev.yml` publica la web y la BD solo en `127.0.0.1` y el servicio `borde` el 80/443 de desarrollo; nunca se despliega.
- Aflojar la política del paquete de despliegue (`deploy/politica.py`), ampliar los archivos que acepta `deploy/entrada.sh` o dar permisos de escritura sobre los scripts de `/usr/local/sbin`: un compose arbitrario equivale a root en un servidor compartido ([ADR 0013](docs/adr/0013-politica-de-seguridad-del-despliegue.md)).
- Conectar la aplicación con `POSTGRES_USER` (administrador): usa siempre `APP_DB_USER`, el rol de mínimo privilegio.
- Guardar la clave de acceso a la web en claro (solo va su hash `AUTH_HASH`, entre comillas simples en el `.env`) ni dejar rutas fuera del Basic Auth salvo `/health` y las públicas del inicio (`/` exacta, `/css/*`, `/js/*` y `/salir` exacta, [ADR 0016](docs/adr/0016-inicio-publico-y-cierre-de-sesion.md)): ninguna debe mostrar datos ni consultar negocios. Al editar el `Caddyfile`, reejecutar `scripts/probar-acceso.ps1` (y `probar-navegador.ps1` si cambian las vistas públicas). Las cabeceras de seguridad se importan también desde `handle_errors`: sin eso el 401 sale sin ninguna.
- Poner `style=`, `<style>`, `<script>` o manejadores `on*=` en las vistas, o recursos de otro origen: la CSP del proxy (`script-src 'self'; style-src 'self'`) los bloquea. No usar `asp-validation-summary` (emite `style="display:none"`): usar el parcial `_ResumenErrores`.
- Versionar una cadena de conexión con contraseña en `appsettings*.json`: llega por entorno, `user-secrets` o un archivo local. Negocios no arranca sin ella.
- Usar `DateTime.Today`, `DateTime.Now` o `CURRENT_DATE` para reglas de negocio: «hoy» es la fecha de `America/Bogota` (`TimeProvider` + `Negocios:ZonaHoraria` en C#, y `now() AT TIME ZONE 'America/Bogota'` en SQL, `db/003`). El servidor y la BD están en UTC.
- Restaurar un archivo con `git checkout --` si hay cambios sin confirmar en él (los descarta); al mutar o probar, restaurar desde una copia del texto original.
- Enviar una clave a `caddy hash-password` por la tubería de PowerShell 5.1 (agrega BOM y CRLF y el hash sale de otra cadena): usar `scripts/generar-hash-auth.ps1`.
- Editar un script SQL ya aplicado.
- Desactivar verificación de host SSH o de certificados para "hacerlo funcionar".

## Despliegue

Plan completo en [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md). Producción ([ADR 0014](docs/adr/0014-despliegue-detras-del-gateway-de-plataforma.md), que reemplaza al 0011, y [ADR 0013](docs/adr/0013-politica-de-seguridad-del-despliegue.md)): el VPS es **compartido** con PortalCV y Colfe; Cloudflare y el **gateway de plataforma** (`/srv/sitiosapps/gateway`, repo `sitiosapps-gateway`, único con el 443, exige mTLS de Cloudflare) van delante y enrutan `gdb-crud.sitiosapps.com` a nuestro Caddy por la red compartida `sitiosapps-edge`. Al fusionar `develop` en `main`, y con los cuatro trabajos del CI en verde, el trabajo `entrega` de `ci.yml` (que llama a `deploy.yml`) publica las imágenes en GHCR y las despliega por SSH con una llave restringida a `/usr/local/sbin/gdb-crud-entrada.sh`; el servidor valida cada entrega con una política de seguridad, hace copia de la base, levanta el stack y vuelve a la versión anterior si falla (código en `deploy/`, probado con `scripts/probar-despliegue.sh`). El proyecto vive en `/srv/sitiosapps/gdb-crud` y su `.env` solo en el servidor. **No tocar el nginx, `daemon.json`, el firewall ni SSH del VPS sin aprobación**: afectan a los otros proyectos.

## Documentos de referencia

- [docs/diagnostico/diagnostico-2026-10-09.md](docs/diagnostico/diagnostico-2026-10-09.md): hallazgos H1–H8 y B1–B8.
- [docs/plan-de-trabajo.md](docs/plan-de-trabajo.md): fases y prioridades.
- [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md): uso de Git, ramas locales y remotas.
- [docs/arquitectura.md](docs/arquitectura.md): vista C4, flujos, seguridad por capas y despliegue (el de producción, implementado en el repositorio; falta su primera publicación).
- [docs/servidor.md](docs/servidor.md): inventario del servidor (carpetas, contenedores, redes, llaves, crons y servicios externos).
- [docs/adr/README.md](docs/adr/README.md): las 17 decisiones de arquitectura con su porqué. **Un ADR aceptado no se edita**: si la decisión cambia, se escribe uno nuevo que lo reemplaza.
- [docs/runbook.md](docs/runbook.md): qué hacer cuando suena cada alerta (sitio caído, alerta del servidor, despliegue fallido, restaurar, cambiar la clave).
- [docs/backups.md](docs/backups.md): copias locales y externas (age + Backblaze B2) y cómo se restauran; [ADR 0015](docs/adr/0015-copia-externa-cifrada-en-b2.md).
- [README.md](README.md): ejecución y decisiones técnicas.

## Estado conocido

- **En producción desde el 2026-10-11** en `https://gdb-crud.sitiosapps.com`, detrás del gateway de plataforma (ADR 0014). Fases 0 a 8 del plan hechas: **H1, H2, H4, H5, H6, H7, B2 (contrato SOAP), B3/B4/B5/B8 (#15), B7 (paginación), B6 (zona horaria), H8 (arquitectura y ADR) y #41 (CHECK de nombre) resueltos**. `main` = producción; el primer despliegue fue el release #57.
- El proyecto compila sin advertencias, corre en modo A y B y tiene 357 pruebas (unitarias, vistas con `WebApplicationFactory`, integración con PostgreSQL real y contrato SOAP real). Cobertura: Negocios 100 %, Presentación 96 % (`scripts/cobertura.ps1`; el CI debe exigir >= 80 % en Negocios). Las mutaciones manuales aplicadas (34 al código, 7 al contrato de B2 y 25 a la paginación de B7) fueron todas detectadas.
- Dependabot configurado (`.github/dependabot.yml`), alertas y correcciones automáticas de seguridad activas.
- Operación (fase 9): backup local diario 02:45 (hora de Berlín) y copia externa mensual cifrada con age en Backblaze B2 (ADR 0015, [docs/backups.md](docs/backups.md)), con restauración probada. Monitoreo: UptimeRobot sobre `/health` (acepta GET y HEAD) y alertas del servidor (`deploy/alertas.sh`, latido a Healthchecks.io, [docs/backups.md](docs/backups.md)). Runbook de operación: [docs/runbook.md](docs/runbook.md). Pendiente: SonarCloud como puerta (#24).
- CI: `.github/workflows/ci.yml` con cuatro trabajos independientes (`ci`, `migrador`, `docker`, `despliegue`) en cada PR y push a `develop` y `main`. Ejecutan los mismos scripts que en local, con acciones fijadas por SHA. No bloquean el botón de fusionar (plan Free, ADR 0005): **no fusionar con un check en rojo**. El CD (`entrega`) despliega a producción en cada push a `main` con todo en verde.
- Decisiones de proceso (D1–D7) en [docs/ciclo-de-vida-desarrollo.md](docs/ciclo-de-vida-desarrollo.md): GitHub por HTTPS con `maomauro`, repo privado, Issues, flujo feature → develop → main, modo B para programar y A antes del PR, hook `pre-push`.
