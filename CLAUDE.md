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
dotnet build -warnaserror
dotnet test                                  # cuando existan los proyectos en tests/
dotnet format --verify-no-changes
docker compose up -d --build                 # stack completo (requiere .env)
docker compose logs -f negocios
```

Desarrollo sin Docker: PostgreSQL local con el script de `db/`, luego `dotnet run` en Negocios y en Presentacion. Secretos con `dotnet user-secrets`, no en `appsettings.json`.

## Convenciones

- Idioma: código de dominio, mensajes de usuario y documentación en **español**; identificadores técnicos (clases de framework, claves de configuración) como los define la plataforma.
- Validación en tres niveles: DataAnnotations (Presentación), `UsuarioValidador` (Negocios, fuente de verdad), `CHECK` en la BD. Un cambio de regla debe tocar los tres.
- Errores de negocio viajan como `FaultException<ErrorServicio>`; nunca exponer detalles de excepciones al cliente.
- SQL siempre parametrizado (Npgsql); sin ORM.
- Sin handlers JavaScript inline en las vistas (ver H1 del diagnóstico); usar atributos `data-*` y `wwwroot/js`.
- Versiones de paquetes fijas (cuando exista `Directory.Packages.props`, se editan allí).
- Cambios de esquema: *expand/contract*, compatibles hacia atrás durante un despliegue.

## Git

Flujo y ramas: ver [docs/git-guia-tecnica.md](docs/git-guia-tecnica.md). Resumen: `main` protegida = producción, ramas cortas `feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/`, PR obligatorio, Conventional Commits en español, **nunca** force-push a `main`. No hacer commit ni push salvo que el usuario lo pida.

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

- El proyecto aún no se ha compilado ni probado con `dotnet` (el diagnóstico fue estático; el SQL sí se probó en PostgreSQL).
- No existen todavía `tests/`, CI/CD, autenticación ni migraciones automáticas; están planificados en el plan de trabajo. Actualizar esta sección cuando se cierren.
