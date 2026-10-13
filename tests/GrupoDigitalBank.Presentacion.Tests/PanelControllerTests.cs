using GrupoDigitalBank.Presentacion.Controllers;
using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>El panel con sesion (/inicio): la pagina de la grilla que pide al servicio, con los limites acotados y los errores mostrados sin excepciones.</summary>
public sealed class PanelControllerTests : IDisposable
{
    private readonly IUsuarioClient _cliente = Substitute.For<IUsuarioClient>();
    private readonly InicioController _controlador = new();

    public void Dispose() => _controlador.Dispose();

    private static UsuarioDto Dto(int id, string nombre = "Ana Perez") =>
        new() { Id = id, Nombre = nombre, FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F" };

    private static IEnumerable<string> Errores(ControllerBase c) =>
        c.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);

    private static PaginaUsuarios Pagina(int pagina, int tamano, int total, params UsuarioDto[] usuarios)
        => new() { Usuarios = [.. usuarios], Total = total, Pagina = pagina, Tamano = tamano };

    [Fact]
    public async Task PanelDevuelveLaPrimeraPaginaDe5PorOmision()
    {
        _cliente.ConsultarPaginaAsync(1, 5).Returns(Pagina(1, 5, 1, Dto(1)));

        var resultado = await _controlador.Panel(_cliente);

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>();
        modelo.Usuarios.ShouldHaveSingleItem().Nombre.ShouldBe("Ana Perez");
        modelo.Total.ShouldBe(1);
        modelo.Pagina.ShouldBe(1);
        modelo.Tamano.ShouldBe(5);
    }

    [Fact]
    public async Task PanelPasaLaPaginaYElTamanoPedidosAlServicio()
    {
        _cliente.ConsultarPaginaAsync(3, 25).Returns(Pagina(3, 25, 60, Dto(51)));

        var resultado = await _controlador.Panel(_cliente, 3, 25);

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
    public async Task PanelConUnTamanoNoPermitidoUsaElTamanoPorOmision(int tamano)
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(Pagina(1, 5, 0));

        await _controlador.Panel(_cliente, 1, tamano);

        await _cliente.Received(1).ConsultarPaginaAsync(1, 5);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100_000, 100_000)]
    [InlineData(100_001, 100_000)]
    [InlineData(int.MaxValue, 100_000)]
    public async Task PanelAcotaLaPaginaALosLimitesDelServicio(int pedida, int esperada)
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(Pagina(1, 10, 0));

        await _controlador.Panel(_cliente, pedida, 10);

        await _cliente.Received(1).ConsultarPaginaAsync(esperada, 10);
    }

    [Fact]
    public async Task PanelMuestraLaPaginaQueDevolvioElServicioNoLaPedida()
    {
        // El servicio devuelve la ultima pagina cuando se pide una fuera de rango
        _cliente.ConsultarPaginaAsync(9, 10).Returns(Pagina(3, 10, 25, Dto(21)));

        var resultado = await _controlador.Panel(_cliente, 9, 10);

        resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>().Pagina.ShouldBe(3);
    }

    [Fact]
    public async Task PanelMuestraPaginaVaciaYElErrorSiElServicioFalla()
    {
        _cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns<PaginaUsuarios>(_ => throw new ServicioException("Sin conexion."));

        var resultado = await _controlador.Panel(_cliente, 2, 25);

        var modelo = resultado.ShouldBeOfType<ViewResult>().Model.ShouldBeOfType<PaginaViewModel>();
        modelo.Usuarios.ShouldBeEmpty();
        modelo.Total.ShouldBe(0);
        Errores(_controlador).ShouldBe(["Sin conexion."]);
    }
}
