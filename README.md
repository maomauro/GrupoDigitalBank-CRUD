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
cp .env.example .env      # ajustar DOMINIO y las DOS contraseñas: POSTGRES_PASSWORD (administrador/migrador) y APP_DB_PASSWORD (rol de la aplicación)
docker compose up -d --build
```

- Producción: `https://<DOMINIO>` (Caddy emite y renueva el certificado).
- Solo el proxy publica puertos; `web`, `negocios` y `db` quedan en la red interna.
- Local sin dominio: usar `DOMINIO=localhost` (Caddy usa su CA interna).
- Los cambios de esquema se aplican con el servicio `migrador` en cada `docker compose up`: ejecuta solo los scripts `db/NNN_*.sql` que falten, en orden y en una transacción cada uno. **Un script ya aplicado no se edita** (el migrador aborta si cambia su checksum): todo cambio es un script nuevo. Ya no hace falta `docker compose down -v` ni se pierden datos al cambiar el esquema. Probar el migrador: `scripts/probar-migrador.ps1`.

- **Roles de PostgreSQL:** `POSTGRES_USER` (administrador) es el propietario y ejecuta las migraciones; la aplicación se conecta con `APP_DB_USER`, un rol que solo puede ejecutar `fn_usuario_crud` (no puede leer la tabla, borrar ni crear objetos). Rotar la contraseña de la aplicación es cambiar `APP_DB_PASSWORD` y volver a desplegar.

Desarrollo local (modos A y B, con `docker-compose.dev.yml`) y pruebas: ver `CLAUDE.md` y `docs/plan-de-trabajo.md` (fase 3.3).

## Decisiones técnicas

- **CoreWCF (SOAP, BasicHttpBinding):** expone la lógica de negocio como servicio con contrato explícito y versionable, manteniendo WCF como estándar de integración sin depender de .NET Framework. Los errores viajan como `FaultException<ErrorServicio>`.
- **ASP.NET Core MVC:** separación clara vista/controlador/modelo, validación con DataAnnotations en servidor y plantillas Razor simples para dos pantallas.
- **PostgreSQL + Npgsql (sin ORM):** control total del SQL y de los tipos; el CRUD se concentra en una función almacenada con parámetro de operación, lo que reduce superficie de SQL en la aplicación y evita inyección (parámetros tipados).
- **Validación en dos niveles:** DataAnnotations en presentación (retroalimentación inmediata) y `UsuarioValidador` en negocios (fuente de verdad), más `CHECK` en la BD.
- **Docker Compose + Caddy:** despliegue reproducible; HTTPS automático sin gestionar certificados a mano.
- **.NET 10 (LTS):** soporte hasta noviembre de 2028.
