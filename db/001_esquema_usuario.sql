-- 001_esquema_usuario.sql
-- Tabla usuario + funcion almacenada de CRUD con parametro de operacion.

CREATE TABLE IF NOT EXISTS usuario (
    id               integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre           varchar(100) NOT NULL,
    fecha_nacimiento date         NOT NULL,
    sexo             char(1)      NOT NULL,
    CONSTRAINT ck_usuario_sexo  CHECK (sexo IN ('M', 'F')),
    CONSTRAINT ck_usuario_fecha CHECK (fecha_nacimiento >= DATE '1900-01-01' AND fecha_nacimiento <= CURRENT_DATE)
);

-- Operaciones (p_operacion):
--   'C' = Crear      (usa p_nombre, p_fecha_nacimiento, p_sexo)
--   'R' = Consultar  (p_id NULL => todos; p_id informado => uno)
--   'U' = Modificar  (usa p_id, p_nombre, p_fecha_nacimiento, p_sexo)
--   'D' = Eliminar   (usa p_id)
-- Devuelve las filas afectadas/consultadas; vacio en U/D indica que el id no existe.
CREATE OR REPLACE FUNCTION fn_usuario_crud(
    p_operacion        char(1),
    p_id               integer     DEFAULT NULL,
    p_nombre           varchar(100) DEFAULT NULL,
    p_fecha_nacimiento date        DEFAULT NULL,
    p_sexo             char(1)     DEFAULT NULL
) RETURNS SETOF usuario
LANGUAGE plpgsql
AS $$
BEGIN
    CASE upper(p_operacion)
        WHEN 'C' THEN
            RETURN QUERY
                INSERT INTO usuario (nombre, fecha_nacimiento, sexo)
                VALUES (p_nombre, p_fecha_nacimiento, upper(p_sexo))
                RETURNING *;
        WHEN 'R' THEN
            RETURN QUERY
                SELECT u.* FROM usuario u
                WHERE p_id IS NULL OR u.id = p_id
                ORDER BY u.id;
        WHEN 'U' THEN
            RETURN QUERY
                UPDATE usuario u
                   SET nombre = p_nombre,
                       fecha_nacimiento = p_fecha_nacimiento,
                       sexo = upper(p_sexo)
                 WHERE u.id = p_id
                RETURNING u.*;
        WHEN 'D' THEN
            RETURN QUERY
                DELETE FROM usuario u
                 WHERE u.id = p_id
                RETURNING u.*;
        ELSE
            RAISE EXCEPTION 'Operacion no valida: %', p_operacion USING ERRCODE = '22023';
    END CASE;
END;
$$;
