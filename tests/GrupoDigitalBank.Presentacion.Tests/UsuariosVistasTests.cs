using System.Text.RegularExpressions;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// Pruebas de las vistas renderizadas de punta a punta (pipeline MVC real, cliente SOAP simulado).
/// Regresion de H1: un nombre malicioso no debe llegar a ningun contexto ejecutable de la pagina.
/// </summary>
public sealed partial class UsuariosVistasTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _fabrica;

    public UsuariosVistasTests(WebApplicationFactory<Program> fabrica) => _fabrica = fabrica;

    // Cualquier atributo de evento en linea (onsubmit=, onclick=, onerror=...) dentro de una etiqueta
    [GeneratedRegex(@"<[^>]+\son[a-z]+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex ManejadorEnLinea();

    // Valores de atributo entre comillas: el texto "onerror=" dentro de un valor codificado es inerte
    [GeneratedRegex("=\"[^\"]*\"")]
    private static partial Regex ValorDeAtributo();

    private static bool TieneManejadorEnLinea(string html)
        => ManejadorEnLinea().IsMatch(ValorDeAtributo().Replace(html, "=\"\""));

    private HttpClient ClienteConUsuario(string nombre)
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarAsync(Arg.Any<int?>()).Returns(
        [
            new UsuarioDto { Id = 7, Nombre = nombre, FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "M" }
        ]);

        return _fabrica.WithWebHostBuilder(b => b.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<IUsuarioClient>();
            servicios.AddSingleton(cliente);
        })).CreateClient();
    }

    public static TheoryData<string> NombresMaliciosos => new()
    {
        "x');alert(1);//",
        "\");alert(1);//",
        "<script>alert(1)</script>",
        "\"><img src=x onerror=alert(1)>",
        "'-alert(1)-'"
    };

    [Theory]
    [MemberData(nameof(NombresMaliciosos))]
    public async Task ConsultarNoGeneraManejadoresEnLineaConNombresMaliciosos(string nombre)
    {
        using var http = ClienteConUsuario(nombre);

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        TieneManejadorEnLinea(html).ShouldBeFalse("la vista no debe usar atributos de evento en linea");
        html.ShouldNotContain("<script>alert", Case.Insensitive);
        html.ShouldNotContain("<img src=x", Case.Insensitive);
    }

    [Fact]
    public async Task ConsultarMuestraElNombreMaliciosoComoTextoCodificado()
    {
        using var http = ClienteConUsuario("x');alert(1);//");

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        // En la celda el apostrofe sale como entidad HTML: se ve como texto, no se ejecuta
        html.ShouldContain("<td>x&#x27;);alert(1);//</td>");
    }

    [Fact]
    public async Task EliminarPideConfirmacionPorAtributoData()
    {
        using var http = ClienteConUsuario("Ana Perez");

        var html = await http.GetStringAsync("/Usuarios/Consultar");

        html.ShouldContain("data-confirmar=\"");
        html.ShouldContain("Ana Perez");
    }

    [Fact]
    public async Task EditarMuestraLosValoresActualesEnElFormulario()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarAsync(7).Returns([new UsuarioDto { Id = 7, Nombre = "Beatriz Gomez", FechaNacimiento = new DateTime(1985, 3, 9), Sexo = "F" }]);
        using var http = _fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        var html = await http.GetStringAsync("/Usuarios/Editar/7");

        html.ShouldContain("value=\"Beatriz Gomez\"");
        html.ShouldContain("value=\"1985-03-09\"");
        html.ShouldContain("name=\"__RequestVerificationToken\"");
    }

    [Fact]
    public async Task LaPaginaDeErrorMuestraUnCodigoDeSeguimientoYNoDetallesInternos()
    {
        using var http = ClienteConUsuario("Ana Perez");

        var html = await http.GetStringAsync("/Usuarios/Error");

        html.ShouldContain("Ocurrió un error");
        html.ShouldContain("Código de seguimiento");
        html.ShouldNotContain("Exception");
        html.ShouldNotContain("StackTrace", Case.Insensitive);
    }

    [Fact]
    public async Task ElLayoutCargaElScriptExterno()
    {
        using var http = ClienteConUsuario("Ana Perez");

        var html = await http.GetStringAsync("/Usuarios/Consultar");
        var respuesta = await http.GetAsync("/js/site.js");

        html.ShouldContain("src=\"/js/site.js\"");
        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldContain("data-confirmar");
    }
}
