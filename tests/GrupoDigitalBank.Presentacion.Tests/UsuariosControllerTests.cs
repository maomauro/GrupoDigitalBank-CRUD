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

    [Fact]
    public async Task ConsultarDevuelveLaVistaConLosUsuarios()
    {
        _cliente.ConsultarAsync(Arg.Any<int?>()).Returns([Dto(1)]);

        var resultado = await _controlador.Consultar();

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<List<UsuarioViewModel>>();
        modelo.ShouldHaveSingleItem().Nombre.ShouldBe("Ana Perez");
    }

    [Fact]
    public async Task ConsultarMuestraListaVaciaYElErrorSiElServicioFalla()
    {
        _cliente.ConsultarAsync(Arg.Any<int?>()).Returns<List<UsuarioDto>>(_ => throw new ServicioException("Sin conexion."));

        var resultado = await _controlador.Consultar();

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<List<UsuarioViewModel>>().ShouldBeEmpty();
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

        resultado.ShouldBeOfType<RedirectToActionResult>().ActionName.ShouldBe(nameof(UsuariosController.Consultar));
        _controlador.TempData["Mensaje"].ShouldBe("Usuario registrado correctamente.");
        await _cliente.Received(1).AgregarAsync(Arg.Is<UsuarioDto>(u => u.Nombre == "Ana Perez" && u.Sexo == "F"));
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

        resultado.ShouldBeOfType<RedirectToActionResult>().ActionName.ShouldBe(nameof(UsuariosController.Consultar));
        _controlador.TempData["Error"].ShouldBe("No existe el usuario 99.");
    }

    [Fact]
    public async Task EditarValidoLlamaAlServicioYRedirigeALaGrilla()
    {
        var resultado = await _controlador.Editar(ModeloValido(5));

        resultado.ShouldBeOfType<RedirectToActionResult>().ActionName.ShouldBe(nameof(UsuariosController.Consultar));
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

        resultado.ShouldBeOfType<RedirectToActionResult>().ActionName.ShouldBe(nameof(UsuariosController.Consultar));
        _controlador.TempData["Mensaje"].ShouldBe("Usuario eliminado correctamente.");
        await _cliente.Received(1).EliminarAsync(7);
    }

    [Fact]
    public async Task EliminarMuestraElErrorDelServicioYRedirige()
    {
        _cliente.EliminarAsync(7).Returns(_ => throw new ServicioException("No existe el usuario 7."));

        var resultado = await _controlador.Eliminar(7);

        resultado.ShouldBeOfType<RedirectToActionResult>().ActionName.ShouldBe(nameof(UsuariosController.Consultar));
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
}
