-- 004_nombre_no_vacio.sql
-- Issue #41: la base aceptaba un nombre vacio o de solo espacios; solo existian CHECK de sexo y fecha. Los
-- validadores de Presentacion y de Negocios lo impedian, pero quien llamara a fn_usuario_crud directamente
-- (otro cliente, un script, un error futuro) podia guardarlo. Este es el tercer nivel de validacion.
--
-- El CHECK exige al menos un caracter que no sea blanco (\S: cualquier cosa salvo espacio, tabulador, salto de
-- linea...), no solo "distinto de ''": un nombre de espacios o de tabuladores tampoco es un nombre. Es mas amplio
-- que btrim(nombre) <> '', que solo recorta espacios. Los blancos Unicode raros (p. ej. espacio de no separacion)
-- dependen de la configuracion regional de la base; los cubre el validador de Negocios (string.IsNullOrWhiteSpace).
--
-- Si ya existieran filas invalidas, el ALTER fallaria con un mensaje generico de PostgreSQL. Se comprueba antes
-- para fallar con un mensaje claro y SIN modificar nada (el migrador aborta y no registra este script). No se
-- corrigen datos automaticamente: decidir que nombre darles es una decision de negocio.

DO $$
DECLARE
    invalidas integer;
    ids       text;
BEGIN
    SELECT count(*), string_agg(id::text, ', ' ORDER BY id)
      INTO invalidas, ids
      FROM usuario
     WHERE nombre !~ '\S';

    IF invalidas > 0 THEN
        RAISE EXCEPTION '004_nombre_no_vacio.sql: % usuario(s) tienen el nombre vacio o en blanco (ids: %). Corrijalos o eliminelos y vuelva a ejecutar el migrador.',
            invalidas, ids
            USING ERRCODE = 'check_violation';
    END IF;
END
$$;

-- Idempotente (como el 003): el migrador puede encontrar una base que ya la tiene pero no tiene el registro
ALTER TABLE usuario DROP CONSTRAINT IF EXISTS ck_usuario_nombre;
ALTER TABLE usuario ADD CONSTRAINT ck_usuario_nombre CHECK (nombre ~ '\S');
