-- 005_paginacion_usuarios.sql
-- Hallazgo B7: la grilla traia TODOS los usuarios en cada consulta. fn_usuario_pagina devuelve una pagina ordenada
-- por id y, en cada fila, el total de la tabla, todo en UNA sola sentencia (misma instantanea: el total y la pagina
-- no pueden contradecirse aunque otro cliente inserte en medio).
--
-- Expand/contract: es una funcion NUEVA; fn_usuario_crud('R') no cambia, de modo que la version anterior de
-- Negocios sigue funcionando durante un despliegue.
--
-- Una pagina fuera de rango (o con la tabla vacia) devuelve UNA fila con id nulo que solo trae el total: asi el
-- llamante sabe cuantas paginas hay y puede pedir la ultima. Los limites se vuelven a validar aqui: Negocios ya lo
-- hace, pero la base no confia en quien la llame.
--
-- Variable de psql (la inyecta db/migrate.sh): :app_user. Mismo esquema de permisos que el script 002.

CREATE OR REPLACE FUNCTION fn_usuario_pagina(
    p_limite         integer,
    p_desplazamiento integer
) RETURNS TABLE (
    id               integer,
    nombre           varchar(100),
    fecha_nacimiento date,
    sexo             char(1),
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
        SELECT pagina.id, pagina.nombre, pagina.fecha_nacimiento, pagina.sexo, cuenta.n
          FROM (SELECT count(*) AS n FROM usuario) AS cuenta
          LEFT JOIN LATERAL (
                SELECT u.id, u.nombre, u.fecha_nacimiento, u.sexo
                  FROM usuario u
                 ORDER BY u.id
                 LIMIT p_limite OFFSET p_desplazamiento
          ) AS pagina ON true;
END;
$$;

-- Nadie por defecto; corre como su propietario con search_path fijo; solo el rol de aplicacion la ejecuta
REVOKE ALL ON FUNCTION fn_usuario_pagina(integer, integer) FROM PUBLIC;

ALTER FUNCTION fn_usuario_pagina(integer, integer)
    SECURITY DEFINER
    SET search_path = public, pg_temp;

SELECT format('GRANT EXECUTE ON FUNCTION fn_usuario_pagina(integer, integer) TO %I', :'app_user') \gexec
