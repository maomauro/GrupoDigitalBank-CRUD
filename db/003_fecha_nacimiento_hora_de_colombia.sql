-- 003_fecha_nacimiento_hora_de_colombia.sql
-- Hallazgo B6: "hoy" para la fecha de nacimiento es la fecha de Colombia, no la del servidor.
--
-- El CHECK original comparaba con CURRENT_DATE, que depende de la zona horaria de la SESION (en el contenedor,
-- UTC). Entre las 19:00 y la medianoche en Colombia (UTC-5) la fecha UTC ya es la de manana y se aceptaba una
-- fecha de nacimiento futura. Ahora la zona va escrita en el propio CHECK, de modo que el limite no cambia con
-- el servidor, el contenedor ni la configuracion de la sesion.
--
-- Colombia no tiene horario de verano (siempre UTC-5). La aplicacion usa la misma regla: Negocios:ZonaHoraria
-- vale America/Bogota por omision. Si alguna vez cambiara la zona de negocio, hay que cambiar AMBOS lugares
-- (un nuevo script numerado aqui y la configuracion de Negocios); este script no se edita.

ALTER TABLE usuario DROP CONSTRAINT IF EXISTS ck_usuario_fecha;

ALTER TABLE usuario ADD CONSTRAINT ck_usuario_fecha CHECK (
    fecha_nacimiento >= DATE '1900-01-01'
    AND fecha_nacimiento <= (now() AT TIME ZONE 'America/Bogota')::date
);
