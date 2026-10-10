using CoreWCF;
using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using GrupoDigitalBank.Negocios.Servicios;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>
/// Capa de negocios: valida, delega en el repositorio y traduce todo error a un fault SOAP con un codigo estable,
/// sin filtrar detalles internos al cliente.
/// </summary>
public class UsuarioServiceTests
{
    private readonly IUsuarioRepositorio _repositorio = Substitute.For<IUsuarioRepositorio>();
    private readonly RegistroDePrueba _log = new();
    private readonly UsuarioService _servicio;

    public UsuarioServiceTests() => _servicio = new UsuarioService(_repositorio, _log);

    private static UsuarioDto Valido(int id = 0) => new()
    {
        Id = id,
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    private static async Task<ErrorServicio> FaultDe(Func<Task> operacion)
    {
        var ex = await Should.ThrowAsync<FaultException<ErrorServicio>>(operacion);
        return ex.Detail;
    }

    // ---------- Agregar ----------

    [Fact]
    public async Task AgregarValidoDelegaEnElRepositorioYDevuelveElCreado()
    {
        var creado = Valido(7);
        _repositorio.CrearAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>()).Returns(creado);

        var resultado = await _servicio.Agregar(Valido());

        resultado.ShouldBeSameAs(creado);
        await _repositorio.Received(1).CrearAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarInvalidoDevuelveFaultDeValidacionConTodosLosDetallesYNoTocaLaBase()
    {
        var invalido = new UsuarioDto { Nombre = "", FechaNacimiento = DateTime.Today.AddDays(1), Sexo = "X" };

        var error = await FaultDe(() => _servicio.Agregar(invalido));

        error.Codigo.ShouldBe("VALIDACION");
        error.Mensaje.ShouldBe("Datos invalidos.");
        error.Detalles.Count.ShouldBe(3);
        await _repositorio.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
    }

    // ---------- Modificar ----------

    [Fact]
    public async Task ModificarValidoDelegaEnElRepositorio()
    {
        var modificado = Valido(5);
        _repositorio.ModificarAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>()).Returns(modificado);

        var resultado = await _servicio.Modificar(Valido(5));

        resultado.ShouldBeSameAs(modificado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ModificarSinIdValidoDevuelveValidacion(int id)
    {
        var error = await FaultDe(() => _servicio.Modificar(Valido(id)));

        error.Codigo.ShouldBe("VALIDACION");
        error.Mensaje.ShouldBe("El id es obligatorio.");
        await _repositorio.DidNotReceiveWithAnyArgs().ModificarAsync(default!, default);
    }

    [Fact]
    public async Task ModificarConDatosInvalidosDevuelveValidacionAntesDeRevisarElId()
    {
        var invalido = Valido(5);
        invalido.Sexo = "X";

        var error = await FaultDe(() => _servicio.Modificar(invalido));

        error.Codigo.ShouldBe("VALIDACION");
        error.Detalles.ShouldContain("El sexo debe ser 'M' o 'F'.");
    }

    [Fact]
    public async Task ModificarInexistenteDevuelveNoEncontradoConElId()
    {
        _repositorio.ModificarAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>()).Returns((UsuarioDto?)null);

        var error = await FaultDe(() => _servicio.Modificar(Valido(42)));

        error.Codigo.ShouldBe("NO_ENCONTRADO");
        error.Mensaje.ShouldContain("42");
    }

    // ---------- Consultar ----------

    [Fact]
    public async Task ConsultarSinIdDevuelveTodosAunqueLaListaEsteVacia()
    {
        _repositorio.ConsultarAsync(null, Arg.Any<CancellationToken>()).Returns([]);

        var lista = await _servicio.Consultar(null);

        lista.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsultarConIdExistenteDevuelveEseUsuario()
    {
        _repositorio.ConsultarAsync(9, Arg.Any<CancellationToken>()).Returns([Valido(9)]);

        var lista = await _servicio.Consultar(9);

        lista.ShouldHaveSingleItem().Id.ShouldBe(9);
    }

    [Fact]
    public async Task ConsultarConIdInexistenteDevuelveNoEncontrado()
    {
        _repositorio.ConsultarAsync(9, Arg.Any<CancellationToken>()).Returns([]);

        var error = await FaultDe(() => _servicio.Consultar(9));

        error.Codigo.ShouldBe("NO_ENCONTRADO");
        error.Mensaje.ShouldContain("9");
    }

    // ---------- Eliminar ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task EliminarSinIdValidoDevuelveValidacion(int id)
    {
        var error = await FaultDe(() => _servicio.Eliminar(id));

        error.Codigo.ShouldBe("VALIDACION");
        await _repositorio.DidNotReceiveWithAnyArgs().EliminarAsync(default, default);
    }

    [Fact]
    public async Task EliminarInexistenteDevuelveNoEncontrado()
    {
        _repositorio.EliminarAsync(3, Arg.Any<CancellationToken>()).Returns(false);

        var error = await FaultDe(() => _servicio.Eliminar(3));

        error.Codigo.ShouldBe("NO_ENCONTRADO");
    }

    [Fact]
    public async Task EliminarExistenteTerminaSinError()
    {
        _repositorio.EliminarAsync(3, Arg.Any<CancellationToken>()).Returns(true);

        await Should.NotThrowAsync(() => _servicio.Eliminar(3));
        await _repositorio.Received(1).EliminarAsync(3, Arg.Any<CancellationToken>());
    }

    // ---------- Errores tecnicos: se registran y NO se filtran al cliente ----------

    [Fact]
    public async Task UnErrorDeBaseDeDatosSeTraduceAFaultGenericoSinFiltrarDetalles()
    {
        var interno = new NpgsqlException("password authentication failed for user usuarios_app host=db");
        _repositorio.CrearAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>()).Returns<UsuarioDto>(_ => throw interno);

        var error = await FaultDe(() => _servicio.Agregar(Valido()));

        error.Codigo.ShouldBe("ERROR_DATOS");
        error.Mensaje.ShouldBe("No fue posible acceder a la base de datos.");
        error.Detalles.ShouldBeEmpty();
        $"{error.Codigo}{error.Mensaje}".ShouldNotContain("password");
        $"{error.Codigo}{error.Mensaje}".ShouldNotContain("usuarios_app");
        _log.Entradas.ShouldContain(e => e.Nivel == LogLevel.Error && ReferenceEquals(e.Excepcion, interno),
            "el detalle tecnico debe quedar en el log, no en la respuesta");
    }

    [Fact]
    public async Task UnErrorInesperadoSeTraduceAFaultGenericoSinFiltrarDetalles()
    {
        var interno = new InvalidOperationException("NullReference en UsuarioRepositorio linea 36");
        _repositorio.ConsultarAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns<List<UsuarioDto>>(_ => throw interno);

        var error = await FaultDe(() => _servicio.Consultar(null));

        error.Codigo.ShouldBe("ERROR_INTERNO");
        error.Mensaje.ShouldBe("Ocurrio un error inesperado.");
        error.Mensaje.ShouldNotContain("NullReference");
        error.Detalles.ShouldBeEmpty();
        _log.Entradas.ShouldContain(e => e.Nivel == LogLevel.Error && ReferenceEquals(e.Excepcion, interno));
    }

    [Fact]
    public async Task LosFaultsDeNegocioNoSeRegistranComoErrores()
    {
        _repositorio.EliminarAsync(3, Arg.Any<CancellationToken>()).Returns(false);

        await FaultDe(() => _servicio.Eliminar(3));

        _log.Entradas.ShouldBeEmpty("un 'no encontrado' es un resultado esperado, no un error del servidor");
    }

    // ---------- Soporte ----------

    private sealed class RegistroDePrueba : ILogger<UsuarioService>
    {
        public List<(LogLevel Nivel, Exception? Excepcion, string Mensaje)> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entradas.Add((logLevel, exception, formatter(state, exception)));
    }
}
