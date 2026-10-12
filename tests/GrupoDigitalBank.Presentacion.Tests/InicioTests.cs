using System.Net;
using System.Text.RegularExpressions;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// El inicio publico (ADR 0016) explica como se desarrollo la solucion y el cierre de sesion hace olvidar las credenciales de
/// Basic Auth. Ninguno consulta el servicio de negocios: por eso el proxy los deja fuera del usuario y clave.
/// </summary>
public sealed partial class InicioTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex(@"<[^>]+\son[a-z]+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex ManejadorEnLinea();

    [GeneratedRegex(@"<style|\sstyle\s*=|<script(?![^>]*\ssrc=)", RegexOptions.IgnoreCase)]
    private static partial Regex EstiloOScriptEnLinea();

    private HttpClient ClienteSinNegocios(out IUsuarioClient cliente)
    {
        cliente = Substitute.For<IUsuarioClient>();
        var sustituto = cliente;
        return fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(sustituto);
        })).CreateClient();
    }

    [Fact]
    public async Task LaRaizEsElInicioYNoLaGrilla()
    {
        using var http = fabrica.CreateClient();

        var respuesta = await http.GetAsync("/");
        var html = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("cómo se desarrolló esta solución");
        html.ShouldContain("Arquitectura");
        html.ShouldContain("CoreWCF");
        html.ShouldNotContain("/Usuarios/Editar/");
    }

    [Theory]
    [InlineData("#enunciado")]
    [InlineData("#arquitectura")]
    [InlineData("#decisiones")]
    [InlineData("#herramientas")]
    [InlineData("#calidad")]
    [InlineData("#seguridad")]
    [InlineData("#produccion")]
    [InlineData("#proceso")]
    [InlineData("#limites")]
    public async Task ElIndiceApuntaASeccionesQueExisten(string ancla)
    {
        using var http = fabrica.CreateClient();

        var html = await (await http.GetAsync("/")).Content.ReadAsStringAsync();

        html.ShouldContain($"href=\"{ancla}\"");
        html.ShouldContain($"id=\"{ancla[1..]}\"");
    }

    [Fact]
    public async Task ElInicioEnlazaALaAplicacion()
    {
        using var http = fabrica.CreateClient();

        var html = await (await http.GetAsync("/")).Content.ReadAsStringAsync();

        html.ShouldContain("href=\"/Usuarios/Consultar\"");
        html.ShouldContain("href=\"/Usuarios/Registrar\"");
    }

    [Fact]
    public async Task ElInicioNoDependeDelServicioDeNegocios()
    {
        using var http = ClienteSinNegocios(out var cliente);

        var respuesta = await http.GetAsync("/");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        await cliente.DidNotReceiveWithAnyArgs().ConsultarAsync(default);
        await cliente.DidNotReceiveWithAnyArgs().ConsultarPaginaAsync(default, default);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/salir")]
    public async Task LasPaginasPublicasNoLlevanCodigoNiEstilosEnLinea(string ruta)
    {
        using var http = fabrica.CreateClient();

        var html = await (await http.GetAsync(ruta)).Content.ReadAsStringAsync();

        ManejadorEnLinea().IsMatch(html).ShouldBeFalse("la CSP del proxy bloquea los manejadores en linea");
        EstiloOScriptEnLinea().IsMatch(html).ShouldBeFalse("la CSP del proxy bloquea <style>, style= y <script> en linea");
    }

    [Fact]
    public async Task SalirRespondeSiempre401ConElDesafioBasicDelMismoRealmYSinCache()
    {
        using var http = fabrica.CreateClient();

        var respuesta = await http.GetAsync("/salir");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        respuesta.Headers.WwwAuthenticate.ToString().ShouldBe("Basic realm=\"restricted\"");
        respuesta.Headers.CacheControl!.NoStore.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldContain("Sesión cerrada");
    }

    [Fact]
    public async Task SalirRespondeIgualAunqueLleguenCredenciales()
    {
        using var http = fabrica.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String("admin:cualquiera"u8.ToArray()));

        var respuesta = await http.GetAsync("/salir");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        respuesta.Headers.WwwAuthenticate.ToString().ShouldContain("Basic");
    }

    [Fact]
    public async Task ElMenuTraeInicioYCerrarSesionEnTodasLasPaginas()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PaginaUsuarios { Usuarios = [], Total = 0, Pagina = 1, Tamano = 10 });
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        foreach (var ruta in new[] { "/", "/Usuarios/Consultar", "/Usuarios/Registrar" })
        {
            var html = await (await http.GetAsync(ruta)).Content.ReadAsStringAsync();

            html.ShouldContain("href=\"/salir\"", customMessage: ruta);
            html.ShouldContain("Cerrar sesión", customMessage: ruta);
            html.ShouldContain("href=\"/\"", customMessage: ruta);
        }
    }
}
