using Npgsql;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>
/// Hallazgo H4: la aplicacion se conecta con un rol de minimo privilegio. Estas pruebas se ejecutan
/// como ese rol y comprueban lo que NO puede hacer (42501 = insufficient_privilege).
/// </summary>
[Trait("Categoria", "Integracion")]
public class RolAplicacionTests(PostgresFixture bd) : IClassFixture<PostgresFixture>
{
    private const string Firma = "fn_usuario_crud(character,integer,character varying,date,character)";

    private async Task<PostgresException> IntentarComoApp(string sql)
    {
        await using var comando = bd.DataSource.CreateCommand(sql);
        return await Should.ThrowAsync<PostgresException>(() => comando.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task NoPuedeBorrarLaTabla()
    {
        var error = await IntentarComoApp("DROP TABLE usuario");

        error.SqlState.ShouldBe("42501");
    }

    [Fact]
    public async Task NoPuedeLeerNiModificarLaTablaDirectamente()
    {
        (await IntentarComoApp("SELECT * FROM usuario")).SqlState.ShouldBe("42501");
        (await IntentarComoApp("INSERT INTO usuario (nombre, fecha_nacimiento, sexo) VALUES ('x', '1990-01-01', 'M')")).SqlState.ShouldBe("42501");
        (await IntentarComoApp("DELETE FROM usuario")).SqlState.ShouldBe("42501");
    }

    [Fact]
    public async Task NoPuedeCrearObjetosNiAlterarLaFuncion()
    {
        (await IntentarComoApp("CREATE TABLE intruso (id int)")).SqlState.ShouldBe("42501");
        (await IntentarComoApp($"DROP FUNCTION {Firma}")).SqlState.ShouldBe("42501");
    }

    [Fact]
    public async Task NoEsSuperusuarioNiPuedeCrearRolesOBases()
    {
        await using var comando = bd.DataSource.CreateCommand(
            "SELECT rolsuper, rolcreatedb, rolcreaterole, rolreplication FROM pg_roles WHERE rolname = current_user");
        await using var lector = await comando.ExecuteReaderAsync();

        (await lector.ReadAsync()).ShouldBeTrue();
        lector.GetBoolean(0).ShouldBeFalse("rolsuper");
        lector.GetBoolean(1).ShouldBeFalse("rolcreatedb");
        lector.GetBoolean(2).ShouldBeFalse("rolcreaterole");
        lector.GetBoolean(3).ShouldBeFalse("rolreplication");
    }

    [Fact]
    public async Task SoloElRolDeAplicacionPuedeEjecutarLaFuncion()
    {
        // Otro rol cualquiera (y PUBLIC) no debe tener EXECUTE sobre la funcion
        await using var crear = bd.AdminDataSource.CreateCommand("CREATE ROLE intruso LOGIN PASSWORD 'x'");
        await crear.ExecuteNonQueryAsync();

        async Task<bool> PuedeEjecutar(string rol)
        {
            await using var c = bd.AdminDataSource.CreateCommand(
                $"SELECT has_function_privilege(@rol, '{Firma}', 'EXECUTE')");
            c.Parameters.AddWithValue("rol", rol);
            return (bool)(await c.ExecuteScalarAsync())!;
        }

        (await PuedeEjecutar(PostgresFixture.UsuarioApp)).ShouldBeTrue();
        (await PuedeEjecutar("intruso")).ShouldBeFalse();
        (await PuedeEjecutar("public")).ShouldBeFalse();
    }

    [Fact]
    public async Task LaFuncionEsSecurityDefinerConSearchPathFijo()
    {
        await using var comando = bd.AdminDataSource.CreateCommand(
            "SELECT prosecdef, proconfig FROM pg_proc WHERE proname = 'fn_usuario_crud'");
        await using var lector = await comando.ExecuteReaderAsync();

        (await lector.ReadAsync()).ShouldBeTrue();
        lector.GetBoolean(0).ShouldBeTrue("SECURITY DEFINER");
        lector.GetFieldValue<string[]>(1).ShouldContain(c => c.StartsWith("search_path=", StringComparison.Ordinal));
    }

    // B7: la funcion de paginacion se protege igual que fn_usuario_crud
    private const string FirmaPagina = "fn_usuario_pagina(integer,integer)";

    [Fact]
    public async Task LaFuncionDePaginacionSoloLaEjecutaElRolDeAplicacion()
    {
        await using var crear = bd.AdminDataSource.CreateCommand("CREATE ROLE intruso_pagina LOGIN PASSWORD 'x'");
        await crear.ExecuteNonQueryAsync();

        async Task<bool> PuedeEjecutar(string rol)
        {
            await using var c = bd.AdminDataSource.CreateCommand(
                $"SELECT has_function_privilege(@rol, '{FirmaPagina}', 'EXECUTE')");
            c.Parameters.AddWithValue("rol", rol);
            return (bool)(await c.ExecuteScalarAsync())!;
        }

        (await PuedeEjecutar(PostgresFixture.UsuarioApp)).ShouldBeTrue();
        (await PuedeEjecutar("intruso_pagina")).ShouldBeFalse();
        (await PuedeEjecutar("public")).ShouldBeFalse();
    }

    [Fact]
    public async Task LaFuncionDePaginacionEsSecurityDefinerConSearchPathFijo()
    {
        await using var comando = bd.AdminDataSource.CreateCommand(
            "SELECT prosecdef, proconfig FROM pg_proc WHERE proname = 'fn_usuario_pagina'");
        await using var lector = await comando.ExecuteReaderAsync();

        (await lector.ReadAsync()).ShouldBeTrue();
        lector.GetBoolean(0).ShouldBeTrue("SECURITY DEFINER");
        lector.GetFieldValue<string[]>(1).ShouldContain(c => c.StartsWith("search_path=", StringComparison.Ordinal));
    }
}
