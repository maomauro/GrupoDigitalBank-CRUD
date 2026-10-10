using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using Npgsql;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>
/// Repositorio y funcion fn_usuario_crud contra PostgreSQL real, conectados como el rol de aplicacion.
/// Las pruebas comparten la base de la clase: cada una usa nombres unicos y no depende del orden ni del conteo.
/// </summary>
[Trait("Categoria", "Integracion")]
public class UsuarioRepositorioIntegracionTests(PostgresFixture bd) : IClassFixture<PostgresFixture>
{
    private readonly UsuarioRepositorio _repositorio = new(bd.DataSource);

    private static string NombreUnico(string prefijo = "Usuario") => $"{prefijo} {Guid.NewGuid():N}"[..Math.Min(100, prefijo.Length + 33)];

    private static UsuarioDto Nuevo(string? nombre = null, string sexo = "F", DateTime? fecha = null) => new()
    {
        Nombre = nombre ?? NombreUnico(),
        FechaNacimiento = fecha ?? new DateTime(1990, 5, 20),
        Sexo = sexo
    };

    private async Task<PostgresException> ErrorAlCrear(UsuarioDto usuario)
        => await Should.ThrowAsync<PostgresException>(() => _repositorio.CrearAsync(usuario));

    // ---------- Crear / Consultar / Eliminar ----------

    [Fact]
    public async Task CrearConsultarYEliminarUnUsuario()
    {
        var creado = await _repositorio.CrearAsync(new UsuarioDto
        {
            Nombre = "Ana Perez",
            FechaNacimiento = new DateTime(1990, 5, 20),
            Sexo = "F"
        });

        creado.Id.ShouldBeGreaterThan(0);
        var encontrado = (await _repositorio.ConsultarAsync(creado.Id)).ShouldHaveSingleItem();
        encontrado.Nombre.ShouldBe("Ana Perez");
        encontrado.Sexo.ShouldBe("F");

        (await _repositorio.EliminarAsync(creado.Id)).ShouldBeTrue();
        (await _repositorio.ConsultarAsync(creado.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CrearRecortaElNombreYNormalizaElSexoAMayuscula()
    {
        var nombre = NombreUnico("Recorte");

        var creado = await _repositorio.CrearAsync(Nuevo("   " + nombre + "   ", sexo: "f"));

        creado.Nombre.ShouldBe(nombre);
        creado.Sexo.ShouldBe("F");
    }

    [Fact]
    public async Task CrearConservaLaFechaDeNacimientoSinDesfaseDeZonaHoraria()
    {
        var creado = await _repositorio.CrearAsync(Nuevo(fecha: new DateTime(1985, 12, 31)));

        creado.FechaNacimiento.ShouldBe(new DateTime(1985, 12, 31));
    }

    [Fact]
    public async Task ConsultarSinIdListaLosUsuariosOrdenadosPorId()
    {
        var creados = new List<int>();
        for (var i = 0; i < 3; i++) creados.Add((await _repositorio.CrearAsync(Nuevo())).Id);

        var todos = await _repositorio.ConsultarAsync(null);

        var ids = todos.Select(u => u.Id).ToList();
        ids.ShouldBe(ids.Order().ToList(), "la lista debe venir ordenada por id");
        creados.ShouldAllBe(id => ids.Contains(id));
    }

    [Fact]
    public async Task ConsultarUnIdInexistenteDevuelveListaVacia()
    {
        (await _repositorio.ConsultarAsync(int.MaxValue)).ShouldBeEmpty();
    }

    [Fact]
    public async Task EliminarDevuelveFalseSiElUsuarioNoExiste()
    {
        var creado = await _repositorio.CrearAsync(Nuevo());

        (await _repositorio.EliminarAsync(creado.Id)).ShouldBeTrue();
        (await _repositorio.EliminarAsync(creado.Id)).ShouldBeFalse("ya no existe");
        (await _repositorio.EliminarAsync(int.MaxValue)).ShouldBeFalse();
    }

    [Fact]
    public async Task EliminarSoloBorraElUsuarioIndicado()
    {
        var a = await _repositorio.CrearAsync(Nuevo());
        var b = await _repositorio.CrearAsync(Nuevo());

        await _repositorio.EliminarAsync(a.Id);

        (await _repositorio.ConsultarAsync(b.Id)).ShouldHaveSingleItem();
    }

    // ---------- Modificar ----------

    [Fact]
    public async Task ModificarCambiaTodosLosCamposYDevuelveElResultado()
    {
        var creado = await _repositorio.CrearAsync(Nuevo(sexo: "F"));
        var nuevoNombre = NombreUnico("Cambiado");

        var modificado = await _repositorio.ModificarAsync(new UsuarioDto
        {
            Id = creado.Id,
            Nombre = nuevoNombre,
            FechaNacimiento = new DateTime(1975, 3, 8),
            Sexo = "m"
        });

        modificado.ShouldNotBeNull();
        modificado.Id.ShouldBe(creado.Id);
        modificado.Nombre.ShouldBe(nuevoNombre);
        modificado.FechaNacimiento.ShouldBe(new DateTime(1975, 3, 8));
        modificado.Sexo.ShouldBe("M");
        (await _repositorio.ConsultarAsync(creado.Id)).ShouldHaveSingleItem().Nombre.ShouldBe(nuevoNombre);
    }

    [Fact]
    public async Task ModificarUnIdInexistenteDevuelveNull()
    {
        var resultado = await _repositorio.ModificarAsync(new UsuarioDto
        {
            Id = int.MaxValue,
            Nombre = "No existe",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Sexo = "M"
        });

        resultado.ShouldBeNull();
    }

    [Fact]
    public async Task ModificarNoAfectaAOtrosUsuarios()
    {
        var a = await _repositorio.CrearAsync(Nuevo());
        var b = await _repositorio.CrearAsync(Nuevo());

        await _repositorio.ModificarAsync(new UsuarioDto { Id = a.Id, Nombre = NombreUnico("Otro"), FechaNacimiento = a.FechaNacimiento, Sexo = "M" });

        (await _repositorio.ConsultarAsync(b.Id)).ShouldHaveSingleItem().Nombre.ShouldBe(b.Nombre);
    }

    // ---------- La base de datos como ultima red: rechaza datos invalidos aunque se salte la validacion ----------

    [Theory]
    [InlineData("X")]
    [InlineData("")]
    [InlineData("MF")]
    public async Task LaBaseRechazaUnSexoInvalido(string sexo)
    {
        var error = await ErrorAlCrear(Nuevo(sexo: sexo));

        // 23514 = check_violation; 22001 = el valor no cabe en char(1)
        error.SqlState.ShouldBeOneOf("23514", "22001");
    }

    [Fact]
    public async Task LaBaseRechazaFechasAnterioresA1900()
    {
        var error = await ErrorAlCrear(Nuevo(fecha: new DateTime(1899, 12, 31)));

        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_usuario_fecha");
    }

    [Fact]
    public async Task LaBaseAceptaLaFechaMinima1900()
    {
        var creado = await _repositorio.CrearAsync(Nuevo(fecha: new DateTime(1900, 1, 1)));

        creado.FechaNacimiento.ShouldBe(new DateTime(1900, 1, 1));
    }

    [Fact]
    public async Task LaBaseRechazaFechasFuturas()
    {
        // +2 dias: evita el borde de zona horaria entre el equipo y el servidor (hallazgo B6)
        var error = await ErrorAlCrear(Nuevo(fecha: DateTime.Today.AddDays(2)));

        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_usuario_fecha");
    }

    [Fact]
    public async Task LaBaseRechazaUnNombreMasLargoQue100Caracteres()
    {
        var error = await ErrorAlCrear(Nuevo(new string('a', 101)));

        error.SqlState.ShouldBe("22001");   // string_data_right_truncation
    }

    [Fact]
    public async Task LaBaseAceptaUnNombreDeExactamente100Caracteres()
    {
        var creado = await _repositorio.CrearAsync(Nuevo(new string('b', 100)));

        creado.Nombre.Length.ShouldBe(100);
    }

    [Fact]
    public async Task LaFuncionRechazaUnaOperacionInvalida()
    {
        await using var comando = bd.DataSource.CreateCommand("SELECT * FROM fn_usuario_crud('X')");

        var error = await Should.ThrowAsync<PostgresException>(async () =>
        {
            await using var lector = await comando.ExecuteReaderAsync();
            await lector.ReadAsync();
        });

        error.SqlState.ShouldBe("22023");   // invalid_parameter_value
        error.MessageText.ShouldContain("Operacion no valida");
    }

    [Theory]
    [InlineData("c")]
    [InlineData("r")]
    public async Task LaFuncionAceptaLaOperacionEnMinuscula(string operacion)
    {
        await using var comando = bd.DataSource.CreateCommand($"SELECT count(*) FROM fn_usuario_crud('{operacion}', NULL, 'Min {Guid.NewGuid():N}', DATE '1990-01-01', 'M')");

        await Should.NotThrowAsync(() => comando.ExecuteScalarAsync());
    }

    [Fact]
    public async Task UnaOperacionFallidaNoDejaDatosAMedias()
    {
        var antes = (await _repositorio.ConsultarAsync(null)).Count;

        await ErrorAlCrear(Nuevo(sexo: "X"));

        (await _repositorio.ConsultarAsync(null)).Count.ShouldBe(antes);
    }
}
