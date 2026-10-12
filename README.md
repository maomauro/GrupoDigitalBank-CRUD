# GrupoDigitalBank-CRUD · Gestión de usuarios

Sistema de gestión de usuarios con **arquitectura por capas en .NET 10**: registro de usuarios (nombre, fecha de nacimiento, sexo) y consulta en grilla con acciones de modificar y eliminar.

## Arquitectura

```
Navegador ──HTTPS──▶ Caddy ──▶ Presentación (ASP.NET Core MVC)
                                   │  SOAP / BasicHttpBinding
                                   ▼
                              Negocios (CoreWCF 1.9.x)  ──Npgsql──▶ PostgreSQL
                                                                      └─ fn_usuario_crud()
```

| Capa | Proyecto | Responsabilidad |
|---|---|---|
| Presentación | `src/GrupoDigitalBank.Presentacion` | Vistas MVC (registro y consulta). Solo conoce el servicio de negocios vía SOAP. |
| Negocios | `src/GrupoDigitalBank.Negocios` | Servicio `UsuarioService` (Agregar, Modificar, Consultar, ConsultarPagina, Eliminar), validaciones y manejo de errores (SOAP faults). **Única capa con acceso a la BD.** |
| Datos | `db/NNN_*.sql` + `db/migrate.sh` | Tabla `usuario` y función `fn_usuario_crud(p_operacion, ...)` (`C`/`R`/`U`/`D`). El servicio `migrador` aplica los scripts pendientes en orden, cada uno en una transacción, y guarda su checksum en `schema_migrations`. |

## Estructura

```
├── db/                             # Scripts SQL numerados (001_...) + migrate.sh (migrador)
├── src/
│   ├── GrupoDigitalBank.Negocios/  # Contratos · Servicios · Validacion · Datos
│   └── GrupoDigitalBank.Presentacion/  # Controllers · Models · Servicios (cliente SOAP) · Views
├── docker-compose.yml              # db + negocios + web + proxy (Caddy); no publica ningún puerto
├── docker-compose.prod.yml         # override de producción: imágenes de GHCR, red compartida del VPS
├── Caddyfile                       # proxy interno: Basic Auth, cabeceras y CSP (sin TLS; ver ADR 0014)
├── deploy/                         # scripts del servidor (llave restringida, política, backup) y vhost de nginx
└── .env.example
```

## Ejecución

Requisitos: Docker + Docker Compose. Este compose **no publica ningún puerto**: en producción el stack va detrás del nginx compartido del VPS ([ADR 0011](docs/adr/0011-despliegue-detras-del-nginx-compartido.md)) y en desarrollo se usa el override `docker-compose.dev.yml`.

```bash
cp .env.example .env      # ajustar DOMINIO, POSTGRES_PASSWORD (administrador/migrador) y APP_DB_PASSWORD (rol de la aplicación)
powershell -ExecutionPolicy Bypass -File scripts/generar-hash-auth.ps1 -ActualizarEnv   # crea AUTH_USUARIO y AUTH_HASH (acceso a la web)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

- Desarrollo: `http://localhost:8080` (sin autenticación; solo accesible desde el equipo).
- Cadena completa de producción en local (nginx con HTTPS autofirmado → Caddy → aplicación, con Basic Auth, cabeceras y CSP): añadir `--profile con-proxy` y abrir `https://localhost` (`DOMINIO=localhost`).
- Producción: `https://gdb-crud.sitiosapps.com` (Cloudflare → nginx compartido del VPS → Caddy). El despliegue es automático al fusionar `develop` en `main` (ver el plan, fase 7).
- Ningún servicio publica puertos en el compose base ni en el de producción; en desarrollo solo se publican en `127.0.0.1`.
- **Salud y seguridad HTTP:** `web` y `negocios` exponen `/health` y Docker los vigila (`docker compose up --wait` espera a que estén sanos; `web` arranca tras `negocios` y el proxy tras `web`). Caddy envía HSTS (`HSTS_MAX_AGE`, 1 año por omisión y 0 en desarrollo), CSP estricta sin nada en línea ni de otro origen y el resto de cabeceras de seguridad, también en los errores 401 y 502. Verificar: `scripts/probar-acceso.ps1` y, en un navegador real, `scripts/probar-navegador.ps1`. El WSDL de Negocios solo se publica en `Development`; su cadena de conexión no se versiona (entorno, `user-secrets` o archivo local).
- **Zona horaria de negocio:** «hoy» (p. ej. para que la fecha de nacimiento no sea futura) es la fecha de `America/Bogota` (UTC-5), no la del servidor, que está en UTC. Se configura con `Negocios__ZonaHoraria` (por omisión `America/Bogota`; el servicio no arranca si el id no es válido) y la base la fija en su `CHECK` (`db/003`).
- **Volúmenes persistentes:** `pgdata` (base de datos), `caddy_data` (estado de Caddy; ya no guarda certificados: el TLS lo termina el nginx) y `dpkeys` (claves de Data Protection de la web: sin ellas, un formulario abierto antes de un redespliegue falla con 400). `docker compose down -v` los borra todos.
- Los cambios de esquema se aplican con el servicio `migrador` en cada `docker compose up`: ejecuta solo los scripts `db/NNN_*.sql` que falten, en orden y en una transacción cada uno. **Un script ya aplicado no se edita** (el migrador aborta si cambia su checksum): todo cambio es un script nuevo. Ya no hace falta `docker compose down -v` ni se pierden datos al cambiar el esquema. Probar el migrador: `scripts/probar-migrador.ps1`.

- **Acceso a la web:** Caddy exige usuario y clave (Basic Auth) en todas las rutas salvo `/health`. Es un solo usuario compartido; la clave nunca se guarda en claro, solo su hash (`AUTH_HASH`) en el `.env`. Generarlo o rotarlo: `scripts/generar-hash-auth.ps1 -ActualizarEnv`. Verificarlo: `scripts/probar-acceso.ps1`. Sin bloqueo por intentos fallidos (ver plan, 4.2): usar claves largas.
- **Roles de PostgreSQL:** `POSTGRES_USER` (administrador) es el propietario y ejecuta las migraciones; la aplicación se conecta con `APP_DB_USER`, un rol que solo puede ejecutar `fn_usuario_crud` (no puede leer la tabla, borrar ni crear objetos). Rotar la contraseña de la aplicación es cambiar `APP_DB_PASSWORD` y volver a desplegar.

Desarrollo local (modos A y B, con `docker-compose.dev.yml`) y pruebas: ver `CLAUDE.md` y `docs/plan-de-trabajo.md` (fase 3.3).

## Documentación

- [Arquitectura](docs/arquitectura.md): contexto, contenedores, componentes, flujos y seguridad por capas (diagramas Mermaid).
- [Decisiones de arquitectura (ADR)](docs/adr/README.md): el porqué de cada decisión importante.
- [Plan de trabajo](docs/plan-de-trabajo.md), [ciclo de vida](docs/ciclo-de-vida-desarrollo.md), [guía de Git](docs/git-guia-tecnica.md) y [diagnóstico inicial](docs/diagnostico/diagnostico-2026-10-09.md).

## Decisiones técnicas

- **CoreWCF (SOAP, BasicHttpBinding):** expone la lógica de negocio como servicio con contrato explícito y versionable, manteniendo WCF como estándar de integración sin depender de .NET Framework. Los errores viajan como `FaultException<ErrorServicio>`.
- **ASP.NET Core MVC:** separación clara vista/controlador/modelo, validación con DataAnnotations en servidor y plantillas Razor simples para dos pantallas.
- **PostgreSQL + Npgsql (sin ORM):** control total del SQL y de los tipos; el CRUD se concentra en una función almacenada con parámetro de operación, lo que reduce superficie de SQL en la aplicación y evita inyección (parámetros tipados).
- **Validación en tres niveles:** DataAnnotations en presentación (retroalimentación inmediata), `UsuarioValidador` en negocios (fuente de verdad) y `CHECK` en la BD (sexo, fecha y nombre no vacío).
- **Docker Compose + Caddy interno:** despliegue reproducible; Caddy aplica Basic Auth, cabeceras de seguridad y CSP, y el TLS lo termina el gateway de plataforma del VPS tras Cloudflare ([ADR 0014](docs/adr/0014-despliegue-detras-del-gateway-de-plataforma.md)).
- **Inicio público (`/`):** una página que explica cómo se desarrolló la solución (arquitectura e infraestructura con diagramas, enlaces al repositorio y a la documentación), con un enlace a la aplicación y «Cerrar sesión» en el menú ([ADR 0016](docs/adr/0016-inicio-publico-y-cierre-de-sesion.md)). La grilla está en `/Usuarios/Consultar`.
- **.NET 10 (LTS):** soporte hasta noviembre de 2028.
