using GrupoDigitalBank.Presentacion.Controllers;
using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>Prueba de arranque con el cliente SOAP simulado. Los casos completos de H7 se añaden en su issue.</summary>
public class UsuariosControllerTests
{
    [Fact]
    public async Task ConsultarDevuelveLaVistaConLosUsuarios()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarAsync(Arg.Any<int?>()).Returns(
        [
            new UsuarioDto { Id = 1, Nombre = "Ana Perez", FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F" }
        ]);
        using var controlador = new UsuariosController(cliente);

        var resultado = await controlador.Consultar();

        var vista = resultado.ShouldBeOfType<ViewResult>();
        var modelo = vista.Model.ShouldBeOfType<List<UsuarioViewModel>>();
        modelo.Count.ShouldBe(1);
        modelo[0].Nombre.ShouldBe("Ana Perez");
    }

    [Fact]
    public async Task ConsultarMuestraListaVaciaSiElServicioFalla()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarAsync(Arg.Any<int?>()).Returns<List<UsuarioDto>>(_ => throw new ServicioException("Sin conexion."));
        using var controlador = new UsuariosController(cliente);

        var resultado = await controlador.Consultar();

        var vista = resultado.ShouldBeOfType<ViewResult>();
        vista.Model.ShouldBeOfType<List<UsuarioViewModel>>().ShouldBeEmpty();
        controlador.ModelState.IsValid.ShouldBeFalse();
    }
}
