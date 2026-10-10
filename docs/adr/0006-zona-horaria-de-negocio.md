# ADR 0006: «Hoy» es la fecha de `America/Bogota`

- **Estado:** Aceptada
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo B6 (issue [#17](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/17)), `db/003_fecha_nacimiento_hora_de_colombia.sql`

## Contexto

La fecha de nacimiento no puede ser futura. «Hoy» se calculaba con `DateTime.Today` del servidor y con `CURRENT_DATE` en la base: ambos siguen la zona del **contenedor y de la sesión, que es UTC**. Colombia es UTC-5, de modo que entre las 19:00 y la medianoche la fecha UTC ya es la de mañana y **se aceptaba una fecha de nacimiento futura**. Reproducido en Docker a las 19:32 hora de Bogotá.

## Decisión

Las reglas de negocio que dependen de «hoy» usan la fecha de **`America/Bogota`** (UTC-5 todo el año, sin horario de verano):

- **En C#**, `UsuarioService` obtiene «hoy» de un `TimeProvider` cuya zona local es la de negocio (`ProveedorDeTiempoConZona`) y se lo pasa al validador, que es una función pura. La zona se configura con `Negocios:ZonaHoraria` (por omisión `America/Bogota`); el host **no arranca** si el id no es válido.
- **En la base**, `ck_usuario_fecha` compara con `(now() AT TIME ZONE 'America/Bogota')::date` en lugar de `CURRENT_DATE`, de modo que no depende de la zona de la sesión.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| Poner `TZ=America/Bogota` en los contenedores | Arregla el síntoma por configuración externa, frágil: un contenedor sin la variable vuelve a fallar sin que nada lo avise |
| `ALTER DATABASE … SET timezone` | Depende de la configuración de la base, no del esquema versionado; un restore la pierde |
| Calcular «hoy» en el cliente web | El navegador puede tener cualquier hora; la fuente de verdad es el servidor |
| Un único lugar de verdad | Hay dos porque la base es la última red de validación; ver consecuencias |

## Consecuencias

- ➕ Determinista y probable: el validador no lee el reloj y el servicio se prueba con `FakeTimeProvider` (borde exacto de la medianoche de Bogotá, cambio de año, año bisiesto).
- ➖ **La zona está duplicada a propósito** (configuración de Negocios y `db/003`, que no se edita). Si la zona de negocio cambiara, hay que cambiar ambos con un script nuevo y la configuración.
- ➖ Límite honesto de las pruebas de base de datos: `now()` no se puede simular y, a media mañana, «hoy en UTC» y «hoy en Bogotá» son la misma fecha; por eso además se prueba la **definición** del `CHECK`.
- ➖ Requiere `tzdata` en la imagen (la de `aspnet:10.0` lo trae; verificado).
- 🔁 Revisar si el negocio pasa a operar en varias zonas: entonces la zona sería un dato por usuario o por sede, no una constante.
