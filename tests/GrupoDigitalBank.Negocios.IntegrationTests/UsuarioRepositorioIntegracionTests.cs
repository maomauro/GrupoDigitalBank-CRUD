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
    public async Task CrearRegistraLaFechaDeAltaYLaModificacionNoLaCambia()
    {
        var antes = DateTime.UtcNow.AddMinutes(-1);
        var creado = await _repositorio.CrearAsync(Nuevo());

        creado.FechaRegistro.Kind.ShouldBe(DateTimeKind.Utc);
        creado.FechaRegistro.ShouldBeInRange(antes, DateTime.UtcNow.AddMinutes(1));

        creado.Nombre = NombreUnico("Cambio");
        var modificado = await _repositorio.ModificarAsync(creado);

        modificado!.FechaRegistro.ShouldBe(creado.FechaRegistro);
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

    // ---------- Hallazgo B6: "hoy" es la fecha de Colombia, no la del servidor ni la de la sesion ----------

    private async Task<DateTime> HoyEnBogota()
    {
        await using var comando = bd.AdminDataSource.CreateCommand("SELECT (now() AT TIME ZONE 'America/Bogota')::date");
        // Npgsql devuelve DateOnly para una columna date
        return ((DateOnly)(await comando.ExecuteScalarAsync())!).ToDateTime(TimeOnly.MinValue);
    }

    [Fact]
    public async Task LaBaseAceptaLaFechaDeHoyEnBogotaYRechazaLaDeManana()
    {
        var hoy = await HoyEnBogota();

        var creado = await _repositorio.CrearAsync(Nuevo(fecha: hoy));
        var error = await ErrorAlCrear(Nuevo(fecha: hoy.AddDays(1)));

        creado.FechaNacimiento.ShouldBe(hoy);
        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_usuario_fecha");
    }

    [Fact]
    public async Task ElCheckDeFechaFijaLaZonaDeColombiaEnSuDefinicion()
    {
        // A media mañana "hoy en UTC" y "hoy en Bogota" son la misma fecha, y ninguna prueba de comportamiento
        // puede distinguirlas hasta la noche. La definicion del CHECK si es determinista.
        await using var comando = bd.AdminDataSource.CreateCommand(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_usuario_fecha'");

        var definicion = (string)(await comando.ExecuteScalarAsync())!;

        definicion.ShouldContain("America/Bogota");
        definicion.ShouldNotContain("CURRENT_DATE", Case.Insensitive, "CURRENT_DATE depende de la zona de la sesion");
        definicion.ShouldContain("1900-01-01");
    }

    // Zonas extremas: en Kiritimati (UTC+14) la fecha local va hasta un dia por delante de Bogota y en
    // Pago_Pago (UTC-11) hasta un dia por detras. El limite no debe moverse con la zona de la sesion.
    [Theory]
    [InlineData("Pacific/Kiritimati", 1)]
    [InlineData("Pacific/Pago_Pago", 1)]
    [InlineData("UTC", 1)]
    public async Task ElLimiteDeFechaFuturaNoDependeDeLaZonaHorariaDeLaSesion(string zonaDeLaSesion, int diasHaciaAdelante)
    {
        var hoy = await HoyEnBogota();
        await using var conexion = await bd.DataSource.OpenConnectionAsync();
        await using (var fijar = new NpgsqlCommand($"SET TIME ZONE '{zonaDeLaSesion}'", conexion))
        {
            await fijar.ExecuteNonQueryAsync();
        }

        async Task<PostgresException?> Insertar(DateTime fecha)
        {
            await using var c = new NpgsqlCommand("SELECT id FROM fn_usuario_crud('C', NULL, @n, @f, 'M')", conexion);
            c.Parameters.AddWithValue("n", NombreUnico("Zona"));
            c.Parameters.AddWithValue("f", NpgsqlTypes.NpgsqlDbType.Date, DateOnly.FromDateTime(fecha));
            try
            {
                await c.ExecuteScalarAsync();
                return null;
            }
            catch (PostgresException ex)
            {
                return ex;
            }
        }

        (await Insertar(hoy)).ShouldBeNull($"hoy en Bogota debe aceptarse aunque la sesion este en {zonaDeLaSesion}");
        var error = await Insertar(hoy.AddDays(diasHaciaAdelante));
        error.ShouldNotBeNull($"manana en Bogota debe rechazarse aunque la sesion este en {zonaDeLaSesion}");
        error.SqlState.ShouldBe("23514");
    }

    // ---------- Nombre vacio (#41): el tercer nivel de validacion. Se llama a la funcion SIN pasar por el
    // repositorio, que recorta el nombre, para probar lo que la base hace por si sola ----------

    private async Task<PostgresException?> LlamarFuncion(string operacion, int? id, string nombre)
    {
        await using var comando = bd.DataSource.CreateCommand(
            "SELECT id FROM fn_usuario_crud(@op, @id, @nombre, DATE '1990-05-20', 'F')");
        comando.Parameters.Add(new NpgsqlParameter("op", NpgsqlTypes.NpgsqlDbType.Char) { Value = operacion });
        comando.Parameters.Add(new NpgsqlParameter("id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)id ?? DBNull.Value });
        comando.Parameters.Add(new NpgsqlParameter("nombre", NpgsqlTypes.NpgsqlDbType.Varchar) { Value = nombre });
        try
        {
            await comando.ExecuteScalarAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t\r\n ")]
    public async Task LaBaseRechazaCrearUnNombreVacioOSoloBlancos(string nombre)
    {
        var error = await LlamarFuncion("C", null, nombre);

        error.ShouldNotBeNull();
        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_usuario_nombre");
    }

    [Fact]
    public async Task LaBaseRechazaModificarUnNombreAVacio()
    {
        var creado = await _repositorio.CrearAsync(Nuevo());

        var error = await LlamarFuncion("U", creado.Id, "   ");

        error.ShouldNotBeNull();
        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_usuario_nombre");
        (await _repositorio.ConsultarAsync(creado.Id)).ShouldHaveSingleItem().Nombre.ShouldBe(creado.Nombre);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Maria Jose Nunez")]
    [InlineData("  con blancos alrededor  ")]
    public async Task LaBaseAceptaCualquierNombreConAlMenosUnCaracterVisible(string nombre)
    {
        var error = await LlamarFuncion("C", null, nombre);

        error.ShouldBeNull();
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
