# Registro de decisiones de arquitectura (ADR)

Cada ADR deja por escrito **qué se decidió, por qué y a qué costo**, para que dentro de seis meses nadie tenga que adivinarlo. Un ADR aceptado **no se edita**: si la decisión cambia, se escribe uno nuevo que lo reemplaza y el anterior pasa a «Reemplazada por».

La vista de conjunto está en [../arquitectura.md](../arquitectura.md).

## Índice

| ADR | Decisión | Estado |
|---|---|---|
| [0001](0001-corewcf-y-soap-entre-capas.md) | CoreWCF y SOAP entre las capas de presentación y negocios | Aceptada |
| [0002](0002-sin-orm-funcion-almacenada.md) | Sin ORM: Npgsql y una función almacenada de CRUD | Aceptada |
| [0003](0003-contrato-soap-duplicado.md) | El contrato SOAP se duplica a mano en Presentación | Aceptada (con riesgo conocido) |
| [0004](0004-autenticacion-minima-basic-auth.md) | Autenticación mínima: Basic Auth en el proxy | Aceptada (provisional) |
| [0005](0005-flujo-de-ramas-y-proteccion-en-github-free.md) | Flujo de ramas feature → develop → main y protección en GitHub Free | Aceptada |
| [0006](0006-zona-horaria-de-negocio.md) | «Hoy» es la fecha de `America/Bogota` | Aceptada |
| [0007](0007-migraciones-propias-con-checksum.md) | Migraciones propias con checksum y un servicio de un solo uso | Aceptada |
| [0008](0008-rol-de-minimo-privilegio.md) | La aplicación se conecta con un rol de mínimo privilegio | Aceptada |
| [0009](0009-cabeceras-http-csp-y-hsts.md) | Cabeceras de seguridad, CSP estricta y HSTS configurable en el proxy | Aceptada |
| [0010](0010-estrategia-de-verificacion.md) | Estrategia de verificación: pruebas, cobertura mínima y mutación | Aceptada |
| [0011](0011-despliegue-detras-del-nginx-compartido.md) | Despliegue detrás del nginx compartido del VPS, con Caddy interno | Reemplazada por 0014 |
| [0012](0012-paginacion-en-el-servidor.md) | Paginación de la grilla en el servidor, con una función nueva | Aceptada |
| [0013](0013-politica-de-seguridad-del-despliegue.md) | Política de seguridad para el paquete de despliegue | Propuesta |
| [0014](0014-despliegue-detras-del-gateway-de-plataforma.md) | Despliegue detrás del gateway de plataforma (reemplaza al 0011) | Aceptada |
| [0015](0015-copia-externa-cifrada-en-b2.md) | Copia externa cifrada con age en Backblaze B2 | Aceptada |
| [0016](0016-inicio-publico-y-cierre-de-sesion.md) | Inicio público y cierre de sesión con Basic Auth | Aceptada |
| [0017](0017-panel-de-usuarios-edad-y-fecha-de-registro.md) | Panel de usuarios con sesión, fecha de registro, edad calculada y paginación de 5 | Aceptada |

## Plantilla

```markdown
# ADR NNNN: título corto y en positivo

- **Estado:** Propuesta | Aceptada | Reemplazada por NNNN
- **Fecha:** AAAA-MM-DD
- **Referencias:** hallazgo, issue o PR

## Contexto
Qué problema o restricción obliga a decidir. Hechos, no opiniones.

## Decisión
Qué se hace, en una o dos frases claras.

## Alternativas consideradas
Qué más se evaluó y por qué no.

## Consecuencias
Lo bueno, lo malo y lo que habrá que vigilar. Cuándo conviene revisar la decisión.
```
