using GrupoDigitalBank.Presentacion.Controllers;
using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// Controlador con el cliente SOAP simulado: cada accion, el camino feliz y los errores del servicio mostrados
/// de forma amigable (sin excepciones sin controlar).
/// </summary>
public sealed class UsuariosControllerTests : IDisposable
{
    private readonly IUsuarioClient _cliente = Substitute.For<IUsuarioClient>();
    private readonly UsuariosController _controlador;

    public UsuariosControllerTests()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "traza-123" };
        _controlador = new UsuariosController(_cliente)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>())
        };
    }

    public void Dispose() => _controlador.Dispose();

    private static UsuarioViewModel ModeloValido(int id = 0) => new()
    {
        Id = id,
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    private static UsuarioDto Dto(int id, string nombre = "Ana Perez") =>
        new() { Id = id, Nombre = nombre, FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F" };

    private static IEnumerable<string> Errores(ControllerBase c) =>
        c.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);

    // ---------- Consultar ----------

    private static PaginaUsuarios Pagina(int pagina, int tamano, int total, params UsuarioDto[] usuarios)
        => new() { Usuarios = [.. usuarios], Total = total, Pagina = pagina, Tamano = tamano };

    [Fact]
    public async Task ConsultarDevuelveLaPrimeraPaginaDe10PorOmision()
    {
        _cliente.ConsultarPaginaAsync(1, 10).Returns(Pagina(1, 10, 1, Dto(1)));

        var resultado = await _controlador.Consultar();

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>();
        modelo.Usuarios.ShouldHaveSingleItem().Nombre.ShouldBe("Ana Perez");
        modelo.Total.ShouldBe(1);
        modelo.Pagina.ShouldBe(1);
        modelo.Tamano.ShouldBe(10);
    }

    [Fact]
    public async Task ConsultarPasaLaPaginaYElTamanoPedidosAlServicio()
    {
        _cliente.ConsultarPaginaAsync(3, 25).Returns(Pagina(3, 25, 60, Dto(51)));

        var resultado = await _controlador.Consultar(3, 25);

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>();
        modelo.Pagina.ShouldBe(3);
        modelo.Tamano.ShouldBe(25);
        modelo.TotalPaginas.ShouldBe(3);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1000)]
    public async Task ConsultarConUnTamanoNoPermitidoUsaElTamanoPorOmision(int tamano)
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(Pagina(1, 10, 0));

        await _controlador.Consultar(1, tamano);

        await _cliente.Received(1).ConsultarPaginaAsync(1, 10);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100_000, 100_000)]
    [InlineData(100_001, 100_000)]
    [InlineData(int.MaxValue, 100_000)]
    public async Task ConsultarAcotaLaPaginaALosLimitesDelServicio(int pedida, int esperada)
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(Pagina(1, 10, 0));

        await _controlador.Consultar(pedida, 10);

        await _cliente.Received(1).ConsultarPaginaAsync(esperada, 10);
    }

    [Fact]
    public async Task ConsultarMuestraLaPaginaQueDevolvioElServicioNoLaPedida()
    {
        // El servicio devuelve la ultima pagina cuando se pide una fuera de rango
        _cliente.ConsultarPaginaAsync(9, 10).Returns(Pagina(3, 10, 25, Dto(21)));

        var resultado = await _controlador.Consultar(9, 10);

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>().Pagina.ShouldBe(3);
    }

    [Fact]
    public async Task ConsultarMuestraPaginaVaciaYElErrorSiElServicioFalla()
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns<PaginaUsuarios>(_ => throw new ServicioException("Sin conexion."));

        var resultado = await _controlador.Consultar(2, 25);

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>();
        modelo.Usuarios.ShouldBeEmpty();
        modelo.Total.ShouldBe(0);
        Errores(_controlador).ShouldBe(["Sin conexion."]);
    }

    // ---------- Registrar ----------

    [Fact]
    public void RegistrarGetMuestraElFormularioVacio()
    {
        var resultado = _controlador.Registrar();

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<UsuarioViewModel>().Nombre.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegistrarValidoLlamaAlServicioYRedirigeALaGrilla()
    {
        var resultado = await _controlador.Registrar(ModeloValido());

        AlInicioConSesion(resultado);
        _controlador.TempData["Mensaje"].ShouldBe("Usuario registrado correctamente.");
        await _cliente.Received(1).AgregarAsync(Arg.Is<UsuarioDto>(u => u.Nombre == "ANA PEREZ" && u.Sexo == "F"));
    }

    [Fact]
    public async Task RegistrarConModeloInvalidoVuelveAlFormularioSinLlamarAlServicio()
    {
        _controlador.ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
        var modelo = ModeloValido();

        var resultado = await _controlador.Registrar(modelo);

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeSameAs(modelo);
        await _cliente.DidNotReceiveWithAnyArgs().AgregarAsync(default!);
    }

    [Fact]
    public async Task RegistrarMuestraCadaDetalleDelErrorDeValidacionDelServicio()
    {
        _cliente.AgregarAsync(Arg.Any<UsuarioDto>())
            .Returns<UsuarioDto>(_ => throw new ServicioException("Datos invalidos.", ["Detalle uno.", "Detalle dos."]));

        var resultado = await _controlador.Registrar(ModeloValido());

        resultado.ShouldBeOfType<ViewResult>();
        Errores(_controlador).ShouldBe(["Detalle uno.", "Detalle dos."]);
    }

    [Fact]
    public async Task RegistrarMuestraElMensajeSiElErrorDelServicioNoTraeDetalles()
    {
        _cliente.AgregarAsync(Arg.Any<UsuarioDto>())
            .Returns<UsuarioDto>(_ => throw new ServicioException("No fue posible comunicarse con el servicio de negocios."));

        await _controlador.Registrar(ModeloValido());

        Errores(_controlador).ShouldBe(["No fue posible comunicarse con el servicio de negocios."]);
    }

    // ---------- Editar ----------

    [Fact]
    public async Task EditarGetMuestraElUsuarioEncontrado()
    {
        _cliente.ConsultarAsync(5).Returns([Dto(5, "Beatriz")]);

        var resultado = await _controlador.Editar(5);

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<UsuarioViewModel>();
        modelo.Id.ShouldBe(5);
        modelo.Nombre.ShouldBe("Beatriz");
    }

    [Fact]
    public async Task EditarGetDeUnUsuarioInexistenteRedirigeConUnMensajeDeError()
    {
        _cliente.ConsultarAsync(99).Returns<List<UsuarioDto>>(_ => throw new ServicioException("No existe el usuario 99."));

        var resultado = await _controlador.Editar(99);

        AlInicioConSesion(resultado);
        _controlador.TempData["Error"].ShouldBe("No existe el usuario 99.");
    }

    [Fact]
    public async Task EditarValidoLlamaAlServicioYRedirigeALaGrilla()
    {
        var resultado = await _controlador.Editar(ModeloValido(5));

        AlInicioConSesion(resultado);
        _controlador.TempData["Mensaje"].ShouldBe("Usuario modificado correctamente.");
        await _cliente.Received(1).ModificarAsync(Arg.Is<UsuarioDto>(u => u.Id == 5));
    }

    [Fact]
    public async Task EditarConModeloInvalidoVuelveAlFormularioSinLlamarAlServicio()
    {
        _controlador.ModelState.AddModelError("Sexo", "El sexo es obligatorio.");

        var resultado = await _controlador.Editar(ModeloValido(5));

        resultado.ShouldBeOfType<ViewResult>();
        await _cliente.DidNotReceiveWithAnyArgs().ModificarAsync(default!);
    }

    [Fact]
    public async Task EditarMuestraLosErroresDelServicio()
    {
        _cliente.ModificarAsync(Arg.Any<UsuarioDto>())
            .Returns<UsuarioDto>(_ => throw new ServicioException("Datos invalidos.", ["La fecha no es valida."]));

        var resultado = await _controlador.Editar(ModeloValido(5));

        resultado.ShouldBeOfType<ViewResult>();
        Errores(_controlador).ShouldBe(["La fecha no es valida."]);
    }

    // ---------- Eliminar ----------

    [Fact]
    public async Task EliminarLlamaAlServicioYRedirigeConUnMensaje()
    {
        var resultado = await _controlador.Eliminar(7);

        AlInicioConSesion(resultado);
        _controlador.TempData["Mensaje"].ShouldBe("Usuario eliminado correctamente.");
        await _cliente.Received(1).EliminarAsync(7);
    }

    [Fact]
    public async Task EliminarVuelveALaMismaPaginaYTamano()
    {
        var resultado = await _controlador.Eliminar(7, 4, 25);

        var redireccion = resultado.ShouldBeOfType<RedirectToActionResult>();
        redireccion.ActionName.ShouldBe(nameof(InicioController.Panel));
        redireccion.ControllerName.ShouldBe("Inicio");
        redireccion.RouteValues.ShouldNotBeNull();
        redireccion.RouteValues["pagina"].ShouldBe(4);
        redireccion.RouteValues["tamano"].ShouldBe(25);
    }

    [Fact]
    public async Task EliminarConErrorTambienVuelveALaMismaPagina()
    {
        _cliente.EliminarAsync(7).Returns(_ => throw new ServicioException("No existe el usuario 7."));

        var resultado = await _controlador.Eliminar(7, 2, 50);

        var redireccion = resultado.ShouldBeOfType<RedirectToActionResult>();
        redireccion.RouteValues.ShouldNotBeNull();
        redireccion.RouteValues["pagina"].ShouldBe(2);
        redireccion.RouteValues["tamano"].ShouldBe(50);
    }

    [Fact]
    public async Task EliminarMuestraElErrorDelServicioYRedirige()
    {
        _cliente.EliminarAsync(7).Returns(_ => throw new ServicioException("No existe el usuario 7."));

        var resultado = await _controlador.Eliminar(7);

        AlInicioConSesion(resultado);
        _controlador.TempData["Error"].ShouldBe("No existe el usuario 7.");
        _controlador.TempData.ContainsKey("Mensaje").ShouldBeFalse();
    }

    // ---------- Error ----------

    [Fact]
    public void ErrorMuestraElCodigoDeSeguimientoSinDetallesInternos()
    {
        var resultado = _controlador.Error();

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBe("traza-123");
    }

    /// <summary>Registrar, modificar y eliminar vuelven al inicio con sesion (grilla + boton de nuevo usuario), no a la pagina suelta de consulta.</summary>
    private static void AlInicioConSesion(IActionResult resultado)
    {
        var redireccion = resultado.ShouldBeOfType<RedirectToActionResult>();
        redireccion.ActionName.ShouldBe(nameof(InicioController.Panel));
        redireccion.ControllerName.ShouldBe("Inicio");
    }
}
