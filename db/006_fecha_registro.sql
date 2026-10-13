-- 006_fecha_registro.sql
-- La grilla muestra cuando se registro cada usuario. Columna nueva con valor por omision: expand/contract, la version
-- anterior de Negocios (que selecciona columnas por nombre) sigue funcionando durante un despliegue.
--
-- Los usuarios que ya existian reciben la fecha de esta migracion (no se conoce su fecha real de alta).
-- fn_usuario_crud devuelve SETOF usuario (INSERT/UPDATE ... RETURNING), de modo que recoge la columna sin cambios.
-- fn_usuario_pagina declara sus columnas: cambia el tipo de retorno, asi que se vuelve a crear con los mismos permisos
-- que el script 005. Variable de psql (la inyecta db/migrate.sh): :app_user.

ALTER TABLE usuario ADD COLUMN IF NOT EXISTS fecha_registro timestamptz NOT NULL DEFAULT now();

DROP FUNCTION IF EXISTS fn_usuario_pagina(integer, integer);

CREATE FUNCTION fn_usuario_pagina(
    p_limite         integer,
    p_desplazamiento integer
) RETURNS TABLE (
    id               integer,
    nombre           varchar(100),
    fecha_nacimiento date,
    sexo             char(1),
    fecha_registro   timestamptz,
    total            bigint
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_limite IS NULL OR p_limite < 1 OR p_limite > 100 THEN
        RAISE EXCEPTION 'Limite no valido: % (debe estar entre 1 y 100)', p_limite USING ERRCODE = '22023';
    END IF;
    IF p_desplazamiento IS NULL OR p_desplazamiento < 0 THEN
        RAISE EXCEPTION 'Desplazamiento no valido: % (no puede ser negativo)', p_desplazamiento USING ERRCODE = '22023';
    END IF;

    RETURN QUERY
        SELECT pagina.id, pagina.nombre, pagina.fecha_nacimiento, pagina.sexo, pagina.fecha_registro, cuenta.n
          FROM (SELECT count(*) AS n FROM usuario) AS cuenta
          LEFT JOIN LATERAL (
                SELECT u.id, u.nombre, u.fecha_nacimiento, u.sexo, u.fecha_registro
                  FROM usuario u
                 ORDER BY u.id
                 LIMIT p_limite OFFSET p_desplazamiento
          ) AS pagina ON true;
END;
$$;

REVOKE ALL ON FUNCTION fn_usuario_pagina(integer, integer) FROM PUBLIC;

ALTER FUNCTION fn_usuario_pagina(integer, integer)
    SECURITY DEFINER
    SET search_path = public, pg_temp;

SELECT format('GRANT EXECUTE ON FUNCTION fn_usuario_pagina(integer, integer) TO %I', :'app_user') \gexec
