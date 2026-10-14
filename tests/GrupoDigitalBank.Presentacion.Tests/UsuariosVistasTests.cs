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
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(
            PaginaCon(new UsuarioDto { Id = 7, Nombre = nombre, FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "M" }));

        return _fabrica.WithWebHostBuilder(b => b.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<IUsuarioClient>();
            servicios.AddSingleton(cliente);
        })).CreateClient();
    }

    private static PaginaUsuarios PaginaCon(params UsuarioDto[] usuarios)
        => new() { Usuarios = [.. usuarios], Total = usuarios.Length, Pagina = 1, Tamano = 10 };

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
    public async Task LaGrillaNoGeneraManejadoresEnLineaConNombresMaliciosos(string nombre)
    {
        using var http = ClienteConUsuario(nombre);

        var html = await http.GetStringAsync("/inicio");

        TieneManejadorEnLinea(html).ShouldBeFalse("la vista no debe usar atributos de evento en linea");
        html.ShouldNotContain("<script>alert", Case.Insensitive);
        html.ShouldNotContain("<img src=x", Case.Insensitive);
    }

    [Fact]
    public async Task LaGrillaMuestraElNombreMaliciosoComoTextoCodificado()
    {
        using var http = ClienteConUsuario("x');alert(1);//");

        var html = await http.GetStringAsync("/inicio");

        // En la celda el apostrofe sale como entidad HTML: se ve como texto, no se ejecuta
        html.ShouldContain("<td>x&#x27;);alert(1);//</td>");
    }

    [Fact]
    public async Task EliminarPideConfirmacionPorAtributoData()
    {
        using var http = ClienteConUsuario("Ana Perez");

        var html = await http.GetStringAsync("/inicio");

        html.ShouldContain("data-confirmar=\"");
        html.ShouldContain("Ana Perez");
    }

    // ---------- Compatibilidad con la politica de contenido (CSP) del proxy ----------
    // El Caddyfile envia: default-src 'self'; script-src 'self'; style-src 'self'. Cualquier script, estilo o
    // manejador en linea, o un recurso de otro origen, dejaria de funcionar en el navegador (hallazgo B4).

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex AtributoStyle();

    [GeneratedRegex(@"<style[\s>]", RegexOptions.IgnoreCase)]
    private static partial Regex EtiquetaStyle();

    [GeneratedRegex(@"<script(?![^>]*\ssrc\s*=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptEnLinea();

    [GeneratedRegex("""\s(?:src|href|action)\s*=\s*"([^"]*)"|\s(?:src|href|action)\s*=\s*'([^']*)'""", RegexOptions.IgnoreCase)]
    private static partial Regex Destinos();

    private HttpClient ClienteQueFalla()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns<PaginaUsuarios>(_ => throw new ServicioException("No fue posible comunicarse con el servicio de negocios."));
        return _fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();
    }

    private HttpClient ClienteVacio()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(PaginaCon());
        return _fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();
    }

    public static TheoryData<string, string> PaginasParaLaCsp => new()
    {
        { "con-usuario", "/inicio" },
        { "vacia", "/inicio" },
        { "con-error", "/inicio" },
        { "con-usuario", "/Usuarios/Registrar" },
        { "con-usuario", "/Usuarios/Editar/7" },
        { "con-usuario", "/Usuarios/Error" }
    };

    [Theory]
    [MemberData(nameof(PaginasParaLaCsp))]
    public async Task NingunaVistaEmiteNadaEnLineaNiApuntaAOtroOrigen(string escenario, string ruta)
    {
        var cliente = Substitute.For<IUsuarioClient>();
        var ana = new UsuarioDto { Id = 7, Nombre = "Ana Perez", FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F" };
        cliente.ConsultarAsync(Arg.Any<int?>()).Returns([ana]);
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(
            new PaginaUsuarios { Usuarios = [ana], Total = 45, Pagina = 2, Tamano = 10 });   // pagina intermedia: con Anterior y Siguiente
        using var http = escenario switch
        {
            "vacia" => ClienteVacio(),
            "con-error" => ClienteQueFalla(),
            _ => _fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IUsuarioClient>();
                s.AddSingleton(cliente);
            })).CreateClient()
        };

        var html = await http.GetStringAsync(ruta);

        AtributoStyle().IsMatch(html).ShouldBeFalse($"{ruta} ({escenario}) emite un atributo style en linea");
        EtiquetaStyle().IsMatch(html).ShouldBeFalse($"{ruta} ({escenario}) emite un bloque <style>");
        ScriptEnLinea().IsMatch(html).ShouldBeFalse($"{ruta} ({escenario}) emite un <script> en linea");
        TieneManejadorEnLinea(html).ShouldBeFalse($"{ruta} ({escenario}) emite un manejador de evento en linea");
        foreach (Match m in Destinos().Matches(html))
        {
            var destino = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            destino.ShouldNotStartWith("http", Case.Insensitive, $"{ruta} ({escenario}) apunta a otro origen: {destino}");
            destino.ShouldNotStartWith("//", Case.Insensitive);
        }
    }

    [Fact]
    public async Task SinErroresNoSeDibujaElRecuadroDeError()
    {
        using var http = ClienteVacio();

        var html = await http.GetStringAsync("/inicio");

        html.ShouldNotContain("alerta error", Case.Insensitive);
        html.ShouldNotContain("data-valmsg-summary");
    }

    [Fact]
    public async Task UnErrorDelServicioSeMuestraComoListaDeAvisos()
    {
        using var http = ClienteQueFalla();

        var html = await http.GetStringAsync("/inicio");

        html.ShouldContain("class=\"alerta error\"");
        html.ShouldContain("<li>No fue posible comunicarse con el servicio de negocios.</li>");
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

        var html = await http.GetStringAsync("/inicio");
        var respuesta = await http.GetAsync("/js/site.js");

        html.ShouldContain("src=\"/js/site.js?v=");
        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldContain("data-confirmar");
    }
}
