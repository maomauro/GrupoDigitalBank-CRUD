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
| Negocios | `src/GrupoDigitalBank.Negocios` | Servicio `UsuarioService` (Agregar, Modificar, Consultar, Eliminar), validaciones y manejo de errores (SOAP faults). **Única capa con acceso a la BD.** |
| Datos | `db/NNN_*.sql` + `db/migrate.sh` | Tabla `usuario` y función `fn_usuario_crud(p_operacion, ...)` (`C`/`R`/`U`/`D`). El servicio `migrador` aplica los scripts pendientes en orden, cada uno en una transacción, y guarda su checksum en `schema_migrations`. |

## Estructura

```
├── db/                             # Scripts SQL numerados (001_...) + migrate.sh (migrador)
├── src/
│   ├── GrupoDigitalBank.Negocios/  # Contratos · Servicios · Validacion · Datos
│   └── GrupoDigitalBank.Presentacion/  # Controllers · Models · Servicios (cliente SOAP) · Views
├── docker-compose.yml              # db + negocios + web + proxy (Caddy)
├── Caddyfile                       # HTTPS automático (Let's Encrypt)
└── .env.example
```

## Ejecución

Requisitos: Docker + Docker Compose (en el VPS, el dominio debe apuntar a su IP y los puertos 80/443 estar abiertos).

```bash
cp .env.example .env      # ajustar DOMINIO, POSTGRES_PASSWORD (administrador/migrador) y APP_DB_PASSWORD (rol de la aplicación)
powershell -ExecutionPolicy Bypass -File scripts/generar-hash-auth.ps1 -ActualizarEnv   # crea AUTH_USUARIO y AUTH_HASH (acceso a la web)
docker compose up -d --build
```

- Producción: `https://<DOMINIO>` (Caddy emite y renueva el certificado).
- Solo el proxy publica puertos; `web`, `negocios` y `db` quedan en la red interna.
- Local sin dominio: usar `DOMINIO=localhost` (Caddy usa su CA interna).
- **Salud y seguridad HTTP:** `web` y `negocios` exponen `/health` y Docker los vigila (`docker compose up --wait` espera a que estén sanos; `web` arranca tras `negocios` y el proxy tras `web`). Caddy envía HSTS (`HSTS_MAX_AGE`, 1 año por omisión y 0 en desarrollo), CSP estricta sin nada en línea ni de otro origen y el resto de cabeceras de seguridad, también en los errores 401 y 502. Verificar: `scripts/probar-acceso.ps1` y, en un navegador real, `scripts/probar-navegador.ps1`. El WSDL de Negocios solo se publica en `Development`; su cadena de conexión no se versiona (entorno, `user-secrets` o archivo local).
- **Zona horaria de negocio:** «hoy» (p. ej. para que la fecha de nacimiento no sea futura) es la fecha de `America/Bogota` (UTC-5), no la del servidor, que está en UTC. Se configura con `Negocios__ZonaHoraria` (por omisión `America/Bogota`; el servicio no arranca si el id no es válido) y la base la fija en su `CHECK` (`db/003`).
- **Volúmenes persistentes:** `pgdata` (base de datos), `caddy_data` (certificados HTTPS) y `dpkeys` (claves de Data Protection de la web: sin ellas, un formulario abierto antes de un redespliegue falla con 400). `docker compose down -v` los borra todos.
- Los cambios de esquema se aplican con el servicio `migrador` en cada `docker compose up`: ejecuta solo los scripts `db/NNN_*.sql` que falten, en orden y en una transacción cada uno. **Un script ya aplicado no se edita** (el migrador aborta si cambia su checksum): todo cambio es un script nuevo. Ya no hace falta `docker compose down -v` ni se pierden datos al cambiar el esquema. Probar el migrador: `scripts/probar-migrador.ps1`.

- **Acceso a la web:** Caddy exige usuario y clave (Basic Auth) en todas las rutas salvo `/health`. Es un solo usuario compartido; la clave nunca se guarda en claro, solo su hash (`AUTH_HASH`) en el `.env`. Generarlo o rotarlo: `scripts/generar-hash-auth.ps1 -ActualizarEnv`. Verificarlo: `scripts/probar-acceso.ps1`. Sin bloqueo por intentos fallidos (ver plan, 4.2): usar claves largas.
- **Roles de PostgreSQL:** `POSTGRES_USER` (administrador) es el propietario y ejecuta las migraciones; la aplicación se conecta con `APP_DB_USER`, un rol que solo puede ejecutar `fn_usuario_crud` (no puede leer la tabla, borrar ni crear objetos). Rotar la contraseña de la aplicación es cambiar `APP_DB_PASSWORD` y volver a desplegar.

Desarrollo local (modos A y B, con `docker-compose.dev.yml`) y pruebas: ver `CLAUDE.md` y `docs/plan-de-trabajo.md` (fase 3.3).

## Decisiones técnicas

- **CoreWCF (SOAP, BasicHttpBinding):** expone la lógica de negocio como servicio con contrato explícito y versionable, manteniendo WCF como estándar de integración sin depender de .NET Framework. Los errores viajan como `FaultException<ErrorServicio>`.
- **ASP.NET Core MVC:** separación clara vista/controlador/modelo, validación con DataAnnotations en servidor y plantillas Razor simples para dos pantallas.
- **PostgreSQL + Npgsql (sin ORM):** control total del SQL y de los tipos; el CRUD se concentra en una función almacenada con parámetro de operación, lo que reduce superficie de SQL en la aplicación y evita inyección (parámetros tipados).
- **Validación en dos niveles:** DataAnnotations en presentación (retroalimentación inmediata) y `UsuarioValidador` en negocios (fuente de verdad), más `CHECK` en la BD.
- **Docker Compose + Caddy:** despliegue reproducible; HTTPS automático sin gestionar certificados a mano.
- **.NET 10 (LTS):** soporte hasta noviembre de 2028.
