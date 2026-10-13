using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>B7: la grilla paginada renderizada de punta a punta (pipeline MVC real, cliente SOAP simulado).</summary>
public sealed class PaginacionVistasTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private static UsuarioDto Usuario(int id)
        => new() { Id = id, Nombre = $"Persona {id}", FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "M" };

    private (HttpClient Http, IUsuarioClient Cliente) Pagina(int pagina, int tamano, int total, int usuarios)
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PaginaUsuarios
        {
            Usuarios = Enumerable.Range(1, usuarios).Select(Usuario).ToList(),
            Total = total,
            Pagina = pagina,
            Tamano = tamano
        });
        var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();
        return (http, cliente);
    }

    // La ruta por omision es Usuarios/Consultar, asi que los enlaces a la grilla se generan como "/?pagina=..."
    [Fact]
    public async Task UnaPaginaIntermediaMuestraDondeEstaYEnlazaALaAnteriorYALaSiguiente()
    {
        var (http, _) = Pagina(pagina: 2, tamano: 10, total: 25, usuarios: 10);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar?pagina=2");

        html.ShouldContain("Página 2 de 3");
        html.ShouldContain("25 usuarios");
        html.ShouldContain("href=\"/Usuarios/Consultar?pagina=1&amp;tamano=10\"");
        html.ShouldContain("href=\"/Usuarios/Consultar?pagina=3&amp;tamano=10\"");
    }

    [Fact]
    public async Task EnLaPrimeraPaginaAnteriorNoEsUnEnlace()
    {
        var (http, _) = Pagina(1, 10, 25, 10);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        html.ShouldContain("<span class=\"deshabilitado\">Anterior</span>");
        html.ShouldContain("Siguiente</a>");
    }

    [Fact]
    public async Task EnLaUltimaPaginaSiguienteNoEsUnEnlace()
    {
        var (http, _) = Pagina(3, 10, 25, 5);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar?pagina=3");

        html.ShouldContain("<span class=\"deshabilitado\">Siguiente</span>");
        html.ShouldContain("Anterior</a>");
    }

    [Fact]
    public async Task ConUnaSolaPaginaNoSeMuestranLosControlesDePaginacion()
    {
        var (http, _) = Pagina(1, 10, 3, 3);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        html.ShouldContain("3 usuarios");
        html.ShouldNotContain("Anterior");
        html.ShouldNotContain("Siguiente");
        html.ShouldNotContain("Página 1 de 1");
        html.ShouldNotContain("Por página");
    }

    [Theory]
    [InlineData(10, new int[0])]
    [InlineData(11, new[] { 10, 25 })]
    [InlineData(25, new[] { 10, 25 })]
    [InlineData(26, new[] { 10, 25, 50 })]
    public void LosTamanosSeOfrecenAMedidaQueCrecenLosUsuarios(int total, int[] esperados)
    {
        GrupoDigitalBank.Presentacion.Models.PaginaViewModel.Desde(new PaginaUsuarios { Usuarios = [], Total = total, Pagina = 1, Tamano = 10 })
            .TamanosOfrecidos.ShouldBe(esperados);
    }

    [Fact]
    public async Task UnSoloUsuarioSeEscribeEnSingular()
    {
        var (http, _) = Pagina(1, 10, 1, 1);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        html.ShouldContain("1 usuario");
        html.ShouldNotContain("1 usuarios");
    }

    [Fact]
    public async Task ElTamanoActualVaSinEnlaceYLosOtrosVuelvenALaPaginaUno()
    {
        var (http, _) = Pagina(3, 25, 120, 25);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar?pagina=3&tamano=25");

        html.ShouldContain("<strong>25</strong>");
        html.ShouldContain("href=\"/Usuarios/Consultar?pagina=1&amp;tamano=10\">10</a>");
        html.ShouldContain("href=\"/Usuarios/Consultar?pagina=1&amp;tamano=50\">50</a>");
        html.ShouldNotContain(">25</a>");
    }

    [Fact]
    public async Task ElFormularioDeEliminarVuelveALaMismaPagina()
    {
        var (http, _) = Pagina(2, 25, 60, 2);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar?pagina=2&tamano=25");

        html.ShouldContain("action=\"/Usuarios/Eliminar/1?pagina=2&amp;tamano=25\"");
        html.ShouldContain("action=\"/Usuarios/Eliminar/2?pagina=2&amp;tamano=25\"");
    }

    [Fact]
    public async Task SinUsuariosNoSeDibujaLaNavegacion()
    {
        var (http, _) = Pagina(1, 10, 0, 0);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        html.ShouldContain("No hay usuarios registrados");
        html.ShouldNotContain("class=\"paginacion\"");
    }

    [Fact]
    public async Task LaPaginaYElTamanoDeLaUrlLleganAlServicio()
    {
        var (http, cliente) = Pagina(3, 25, 120, 25);
        using var _ = http;

        await http.GetStringAsync("/Usuarios/Consultar?pagina=3&tamano=25");

        await cliente.Received(1).ConsultarPaginaAsync(3, 25);
    }

    [Fact]
    public async Task UnaUrlConValoresQueNoSonNumerosSeTrataComoLaPrimeraPagina()
    {
        var (http, cliente) = Pagina(1, 10, 5, 5);
        using var _ = http;

        var respuesta = await http.GetAsync("/Usuarios/Consultar?pagina=abc&tamano=xyz");

        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        await cliente.Received(1).ConsultarPaginaAsync(1, 10);
    }

    [Fact]
    public async Task UnaPaginaAbsurdamenteGrandeNoLlegaSinAcotarAlServicio()
    {
        var (http, cliente) = Pagina(1, 10, 5, 5);
        using var _ = http;

        await http.GetStringAsync("/Usuarios/Consultar?pagina=2147483647&tamano=999999");

        await cliente.Received(1).ConsultarPaginaAsync(100_000, 10);
    }

    [Fact]
    public async Task LaNavegacionNoEmiteNadaEnLineaQueLaCspBloquee()
    {
        var (http, _) = Pagina(2, 10, 25, 10);
        using var _ = http;

        var html = await http.GetStringAsync("/Usuarios/Consultar?pagina=2");

        html.ShouldNotContain(" style=", Case.Insensitive);
        html.ShouldNotContain("onclick=", Case.Insensitive);
        html.ShouldNotContain("<script>", Case.Insensitive);
    }
}
