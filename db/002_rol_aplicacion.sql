-- 002_rol_aplicacion.sql
-- Rol de aplicacion con minimo privilegio (hallazgo H4).
--
-- La aplicacion ya no se conecta como propietario/superusuario: usa un rol que solo puede
-- EJECUTAR fn_usuario_crud. No puede leer ni modificar la tabla directamente, crear objetos
-- ni borrar nada. La funcion corre con los privilegios de su propietario (SECURITY DEFINER).
--
-- Variables de psql (las inyecta db/migrate.sh desde APP_DB_USER y APP_DB_PASSWORD):
--   :app_user  nombre del rol de aplicacion
--   :app_pass  su contrasena (nunca se versiona; llega por entorno)
--
-- Se usa \gexec con format(%I, %L) porque psql no interpola variables dentro de $$...$$
-- y asi el nombre y la contrasena quedan correctamente entrecomillados.

-- 1) Rol: se crea si no existe y siempre se (re)aplican sus atributos y la contrasena
SELECT format('CREATE ROLE %I LOGIN', :'app_user')
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user') \gexec

SELECT format('ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
              :'app_user', :'app_pass') \gexec

-- 2) Nadie por defecto: se retiran los permisos que PUBLIC recibe en PostgreSQL
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database()) \gexec
REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON TABLE usuario FROM PUBLIC;
REVOKE ALL ON FUNCTION fn_usuario_crud(character, integer, character varying, date, character) FROM PUBLIC;

-- 3) La funcion corre como su propietario; search_path fijo evita el secuestro de nombres
ALTER FUNCTION fn_usuario_crud(character, integer, character varying, date, character)
    SECURITY DEFINER
    SET search_path = public, pg_temp;

-- 4) Lo unico que recibe el rol de aplicacion
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'app_user') \gexec
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'app_user') \gexec
SELECT format('GRANT EXECUTE ON FUNCTION fn_usuario_crud(character, integer, character varying, date, character) TO %I',
              :'app_user') \gexec
