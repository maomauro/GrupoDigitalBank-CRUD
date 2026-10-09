# Diagnóstico: GrupoDigitalBank-CRUD

Fecha: 2026-10-09

Alcance: revisión estática de todo el código, Docker/Compose, Caddy, SQL y README. No se compiló el proyecto (sin `dotnet` disponible). El script SQL se ejecutó contra un PostgreSQL 16 real: las operaciones C/R/U/D y el error por operación inválida funcionan.

## Resumen

El proyecto cumple la especificación funcional y el stack decidido (.NET 10, CoreWCF 1.9.x con BasicHttpBinding, Npgsql sin ORM, PostgreSQL con `fn_usuario_crud`, Docker Compose + Caddy con HTTPS).

## Qué está bien

- **Capas:** Presentación no referencia a Negocios y solo habla SOAP. Negocios es la única capa con acceso a la BD.
- **Validación:** tres niveles (DataAnnotations, `UsuarioValidador`, `CHECK` en la BD). Errores como `FaultException<ErrorServicio>`, sin filtrar detalles de excepciones.
- **Despliegue:** solo Caddy publica puertos; HTTPS automático; contenedores sin root; antiforgery en formularios.
- **Documentación:** README con estructura, ejecución y justificación de CoreWCF, MVC y PostgreSQL.

## Hallazgos

### Alta

1. **XSS almacenado en el botón Eliminar** (`Views/Usuarios/Consultar.cshtml`). El nombre se inserta en `confirm('¿Eliminar a @u.Nombre?')` dentro de `onsubmit`. Razor codifica `'` como `&#x27;`, pero el navegador lo decodifica antes de ejecutar el JS. Un nombre como `x');alert(1);//` (cabe en 100 caracteres) ejecuta código al cargar la grilla. Solución: mensaje genérico o `data-nombre` con un listener.
2. **Sin autenticación.** Expuesto a internet, cualquiera puede crear, modificar o borrar usuarios. No está en la especificación, pero el estándar OWASP/auth lo trata como sección fija. Mínimo: Basic Auth en Caddy o login.
3. **No es un repositorio Git** y el objetivo incluye GitHub.

### Media

4. **La app se conecta como superusuario de Postgres.** Falta un rol de aplicación con permiso solo de `EXECUTE` sobre la función (y sobre la tabla, si no se usa `SECURITY DEFINER`).
5. **Sin estrategia de migraciones.** `docker-entrypoint-initdb.d` solo corre al crear el volumen; cualquier cambio de esquema obliga a `down -v`, que borra los datos. Faltan scripts `002_…` aplicados con una herramienta (DbUp/Flyway) y backups.
6. **Claves de Data Protection sin persistir.** Al redesplegar se pierden y fallan antiforgery y `TempData` en vuelo. Requiere un volumen para las claves.
7. **Sin tests ni CI/CD.** No hay proyecto de pruebas ni workflow de GitHub Actions; dificulta sostener "código limpio" y las métricas DORA.
8. **`docs/` vacío** (y `.dockerignore` lo excluye). Faltan arc42/ADRs/C4, o quitar la carpeta.

### Baja

- Versiones flotantes (`1.9.*`, `10.*`, `8.*`): builds no reproducibles. Fijarlas con `Directory.Packages.props`.
- Contrato SOAP duplicado a mano en `ContratoServicio.cs`: riesgo de desfase. Compromiso aceptable; merece un ADR.
- Compose sin `healthcheck` en `negocios` y `web` (ya existe `/health`); el `depends_on` del proxy no espera a que `web` esté sano.
- Faltan cabeceras de seguridad (HSTS, CSP, `X-Content-Type-Options`) en Caddy.
- `appsettings.json` versiona una contraseña por defecto (placeholder, pero mala práctica).
- Límites de fecha con `DateTime.Today` / `CURRENT_DATE` dependen de la zona horaria del servidor (UTC en el contenedor).
- La grilla no tiene paginación (aceptable a esta escala).
- WSDL habilitado siempre; solo es accesible por la red interna, pero podría activarse solo en desarrollo.
