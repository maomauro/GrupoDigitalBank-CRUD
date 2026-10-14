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
    [InlineData("#codigo")]
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

        html.ShouldContain("href=\"/inicio\"");
        html.ShouldNotContain("Registrar un usuario");
        html.ShouldNotContain("href=\"/Usuarios/Registrar\"");
    }

    [Fact]
    public async Task ElInicioEnlazaAlRepositorioYTraeLosDosDiagramas()
    {
        using var http = fabrica.CreateClient();

        var html = await (await http.GetAsync("/")).Content.ReadAsStringAsync();

        html.ShouldContain("href=\"https://github.com/maomauro/GrupoDigitalBank-CRUD\"");
        html.ShouldContain("Arquitectura por capas");
        html.ShouldContain("Infraestructura de producción");
        html.ShouldContain("<svg");
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
        var html = await respuesta.Content.ReadAsStringAsync();
        html.ShouldContain("Sesión cerrada");
        html.ShouldContain("http-equiv=\"refresh\" content=\"0;url=/?sesion=cerrada\"");
    }

    [Fact]
    public async Task TrasCerrarSesionElInicioPublicoMuestraElAviso()
    {
        using var http = fabrica.CreateClient();

        var con = await http.GetStringAsync("/?sesion=cerrada");
        var sin = await http.GetStringAsync("/");
        var otro = await http.GetStringAsync("/?sesion=<script>alert(1)</script>");

        con.ShouldContain("Sesión cerrada. Para volver a entrar");
        con.ShouldContain("data-autocierre=\"5000\"");
        sin.ShouldNotContain("Sesión cerrada. Para volver a entrar");
        otro.ShouldNotContain("Sesión cerrada. Para volver a entrar");
        otro.ShouldNotContain("<script>alert(1)</script>");
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
    public async Task SinSesionElMenuMuestraSoloIniciarSesion()
    {
        using var http = fabrica.CreateClient();

        foreach (var ruta in new[] { "/", "/salir" })
        {
            var menu = Cabecera(await (await http.GetAsync(ruta)).Content.ReadAsStringAsync());

            menu.ShouldContain("Grupo Digital Bank", customMessage: ruta);
            menu.ShouldContain("Iniciar sesión", customMessage: ruta);
            menu.ShouldNotContain("Gestión de usuarios", customMessage: ruta);
            menu.ShouldNotContain("Cerrar sesión", customMessage: ruta);
            menu.ShouldNotContain(">Inicio</a>", customMessage: ruta);
        }
    }

    [Fact]
    public async Task CerrarSesionPideConfirmacionAntesDeSalir()
    {
        using var http = ClienteConGrilla(out _);

        var menu = Cabecera(await http.GetStringAsync("/inicio"));
        var script = await http.GetStringAsync("/js/site.js");

        menu.ShouldContain("class=\"salir\"");
        menu.ShouldContain("data-confirmar=\"¿Cerrar sesión?");
        menu.ShouldContain("href=\"/salir\"");
        script.ShouldContain("a[data-confirmar]");
        script.ShouldContain("window.confirm");
        script.ShouldContain("preventDefault");
    }

    [Fact]
    public async Task CerrarSesionNoMuestraElCuadroDelNavegadorLaCierraEnSegundoPlano()
    {
        using var http = ClienteConGrilla(out _);

        var menu = Cabecera(await http.GetStringAsync("/inicio"));
        var script = await http.GetStringAsync("/js/site.js");

        menu.ShouldContain("data-cerrar-sesion");
        menu.ShouldContain("data-destino=\"/?sesion=cerrada\"");
        script.ShouldContain("XMLHttpRequest");
        script.ShouldContain("data-cerrar-sesion");
        script.ShouldContain("window.location.assign");
    }

    [Fact]
    public async Task TrasElAvisoDeSesionCerradaLaDireccionQuedaLimpia()
    {
        using var http = fabrica.CreateClient();

        var script = await http.GetStringAsync("/js/site.js");

        script.ShouldContain("?sesion=cerrada");
        script.ShouldContain("history.replaceState");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    [Fact]
    public async Task ElAvisoDeUsuarioRegistradoSeDesvaneceA5SegundosPeroLosErroresSeQuedan()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PaginaUsuarios { Usuarios = [], Total = 0, Pagina = 1, Tamano = 10 });
        cliente.EliminarAsync(Arg.Any<int>()).Returns<Task>(_ => throw new ServicioException("No se pudo eliminar."));
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        // Registrar: el aviso de exito se oculta solo
        var token = TokenAntiforgery().Match(await http.GetStringAsync("/Usuarios/Registrar")).Groups[1].Value;
        var registro = await http.PostAsync("/Usuarios/Registrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Ana Torres",
            ["FechaNacimiento"] = "1990-05-20",
            ["Sexo"] = "F",
            ["__RequestVerificationToken"] = token
        }));
        var html = await registro.Content.ReadAsStringAsync();
        html.ShouldContain("data-autocierre=\"5000\">Usuario registrado correctamente.");

        // Un error NO se oculta solo: hay que poder leerlo
        token = TokenAntiforgery().Match(await http.GetStringAsync("/Usuarios/Registrar")).Groups[1].Value;
        var eliminar = await http.PostAsync("/Usuarios/Eliminar/7", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var conError = await eliminar.Content.ReadAsStringAsync();
        conError.ShouldContain("No se pudo eliminar.");
        conError.ShouldContain("class=\"alerta error\"");
        conError.ShouldNotContain("class=\"alerta error\" role=\"alert\" data-autocierre");
        conError.ShouldNotContain("data-autocierre=\"5000\">No se pudo eliminar.");
    }

    [Fact]
    public async Task ElAvisoDeSesionCerradaSeDesvaneceSoloALos5Segundos()
    {
        using var http = fabrica.CreateClient();

        var script = await http.GetStringAsync("/js/site.js");
        var css = await http.GetStringAsync("/css/site.css");

        script.ShouldContain("[data-autocierre]");
        script.ShouldContain("desvaneciendo");
        css.ShouldContain(".alerta.desvaneciendo");
    }

    [Fact]
    public async Task ConSesionElMenuMuestraInicioYCerrarSesion()
    {
        using var http = ClienteConGrilla(out _);

        foreach (var ruta in new[] { "/inicio", "/Usuarios/Registrar" })
        {
            var menu = Cabecera(await (await http.GetAsync(ruta)).Content.ReadAsStringAsync());

            menu.ShouldContain("class=\"marca\" href=\"/acerca\"", customMessage: ruta);
            menu.ShouldContain("class=\"app\" href=\"/inicio\">Gestión de usuarios</a>", customMessage: ruta);
            menu.ShouldContain("href=\"/salir\"", customMessage: ruta);
            menu.ShouldContain("Cerrar sesión", customMessage: ruta);
            menu.ShouldNotContain("Iniciar sesión", customMessage: ruta);
            menu.ShouldNotContain(">Inicio</a>", customMessage: ruta);
        }
    }

    /// <summary>Solo el menu superior: el cuerpo de algunas paginas menciona "Cerrar sesión" al explicar sus limites.</summary>
    private static string Cabecera(string html)
    {
        var inicio = html.IndexOf("<header>", StringComparison.Ordinal);
        var fin = html.IndexOf("</header>", StringComparison.Ordinal);
        (inicio >= 0 && fin > inicio).ShouldBeTrue("la pagina debe tener cabecera");
        return html[inicio..fin];
    }

    private HttpClient ClienteConGrilla(out IUsuarioClient cliente)
    {
        cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PaginaUsuarios
        {
            Usuarios = [new UsuarioDto { Id = 7, Nombre = "Ana Torres", FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F", Edad = 36, FechaRegistro = new DateTime(2026, 10, 9, 21, 30, 0) }],
            Total = 1,
            Pagina = 1,
            Tamano = 10
        });
        var sustituto = cliente;
        return fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(sustituto);
        })).CreateClient();
    }

    [Fact]
    public async Task ElInicioConSesionMuestraElBotonDeNuevoUsuarioSobreLaGrillaYNoLaExplicacion()
    {
        using var http = ClienteConGrilla(out _);

        var html = await (await http.GetAsync("/inicio")).Content.ReadAsStringAsync();

        html.ShouldContain("Usuarios registrados");
        html.ShouldContain("Ana Torres");
        var boton = html.IndexOf("Nuevo usuario", StringComparison.Ordinal);
        boton.ShouldBeGreaterThan(-1);
        html.ShouldContain("href=\"/Usuarios/Registrar\"");
        boton.ShouldBeLessThan(html.IndexOf("<table", StringComparison.Ordinal), "el boton va SOBRE la grilla");
        html.ShouldNotContain("cómo se desarrolló esta solución");
    }

    [Fact]
    public async Task LaGrillaMuestraEdadFechaDeRegistroYIconosEnLugarDeTextos()
    {
        using var http = ClienteConGrilla(out _);

        var html = await (await http.GetAsync("/inicio")).Content.ReadAsStringAsync();

        html.ShouldContain("<th>Edad</th>");
        html.ShouldContain("<th>Fecha de registro</th>");
        html.ShouldContain("<td>36 años</td>");
        html.ShouldContain("<td>2026-10-09 21:30</td>");
        html.ShouldContain("title=\"Modificar\"");
        html.ShouldContain("title=\"Eliminar\"");
        html.ShouldContain("title=\"Nuevo usuario\"");
        html.ShouldContain("<svg class=\"icono\"");
        html.ShouldNotContain(">Modificar</a>");
        html.ShouldNotContain(">Eliminar</button>");
        html.ShouldNotContain("style=");
    }

    [Fact]
    public async Task ElFormularioMuestraLaEdadCalculadaDeLaFechaDeNacimiento()
    {
        using var http = ClienteConGrilla(out _);

        var html = await http.GetStringAsync("/Usuarios/Registrar");
        var script = await http.GetStringAsync("/js/site.js");

        html.ShouldContain("data-edad-destino=\"edad-calculada\"");
        html.ShouldContain("id=\"edad-calculada\"");
        html.ShouldNotContain("style=");
        script.ShouldContain("America/Bogota");
        script.ShouldContain("data-edad-destino");
        script.ShouldContain("entrada.max");
    }

    [Fact]
    public async Task ElCampoNombreSoloAdmiteMayusculas()
    {
        using var http = ClienteConGrilla(out _);

        var html = await http.GetStringAsync("/Usuarios/Registrar");
        var script = await http.GetStringAsync("/js/site.js");
        var css = await http.GetStringAsync("/css/site.css");

        html.ShouldContain("class=\"solo-mayusculas\"");
        script.ShouldContain("toUpperCase");
        css.ShouldContain("text-transform: uppercase");
        new GrupoDigitalBank.Presentacion.Models.UsuarioViewModel { Nombre = "  ana maría núñez ", FechaNacimiento = new DateTime(1990, 1, 1), Sexo = "F" }
            .ADto().Nombre.ShouldBe("ANA MARÍA NÚÑEZ");
    }

    [Fact]
    public async Task ElIndiceDeLaExplicacionVaEnUnaLineaYQuedaFijoAlDesplazarse()
    {
        using var http = ClienteConGrilla(out _);

        var css = await http.GetStringAsync("/css/site.css");

        css.ShouldContain(".indice { position: sticky; top: 0;");
        css.ShouldContain("flex-wrap: nowrap");
        css.ShouldContain("scroll-margin-top");
    }

    [Fact]
    public async Task LaExplicacionIncluyeUnGlosarioDeSiglasEnlazadoDesdeElIndice()
    {
        using var http = ClienteConGrilla(out _);

        var html = await http.GetStringAsync("/");

        html.ShouldContain("href=\"#glosario\"");
        html.ShouldContain("id=\"glosario\"");
        html.ShouldContain("<em>Architecture Decision Record</em>");
        html.ShouldContain("<strong>mTLS</strong>");
    }

    [Fact]
    public async Task ElTituloDelMenuAbreLaExplicacionPublicaSinSesionYLaProtegidaConSesion()
    {
        using var http = ClienteConGrilla(out _);

        var sinSesion = Cabecera(await (await http.GetAsync("/")).Content.ReadAsStringAsync());
        var conSesion = Cabecera(await (await http.GetAsync("/inicio")).Content.ReadAsStringAsync());

        sinSesion.ShouldContain("class=\"marca\" href=\"/\"");
        conSesion.ShouldContain("class=\"marca\" href=\"/acerca\"");
    }

    [Fact]
    public async Task AcercaMuestraLaExplicacionCompletaConElMenuDeSesion()
    {
        using var http = fabrica.CreateClient();

        var html = await (await http.GetAsync("/acerca")).Content.ReadAsStringAsync();
        var menu = Cabecera(html);

        html.ShouldContain("cómo se desarrolló esta solución");
        html.ShouldContain("Arquitectura por capas");
        html.ShouldContain("Infraestructura de producción");
        html.ShouldNotContain("Ir a la aplicación");
        html.ShouldNotContain("usa las credenciales enviadas");
        menu.ShouldContain("Cerrar sesión");
        menu.ShouldNotContain("Iniciar sesión");
    }

    [Fact]
    public async Task LaPaginacionDelInicioConSesionSeQuedaEnElInicio()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(2, 5).Returns(new PaginaUsuarios
        {
            Usuarios = [new UsuarioDto { Id = 11, Nombre = "Laura Gómez", FechaNacimiento = new DateTime(1998, 3, 14), Sexo = "F" }],
            Total = 25,
            Pagina = 2,
            Tamano = 10
        });
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        var html = await http.GetStringAsync("/inicio?pagina=2");

        html.ShouldContain("href=\"/inicio?pagina=1&amp;tamano=10\"");
        html.ShouldContain("href=\"/inicio?pagina=3&amp;tamano=10\"");
    }

    [Theory]
    [InlineData("abc", "xyz")]
    [InlineData("-5", "999999")]
    public async Task ElInicioConSesionNoFallaConParametrosDePaginaInvalidos(string pagina, string tamano)
    {
        using var http = ClienteConGrilla(out var cliente);

        var respuesta = await http.GetAsync($"/inicio?pagina={pagina}&tamano={tamano}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        await cliente.Received().ConsultarPaginaAsync(1, 5);
    }

    [Fact]
    public async Task ElInicioConSesionMuestraElErrorYNoSeCaeSiNegociosFalla()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarPaginaAsync(Arg.Any<int>(), Arg.Any<int>()).Returns<PaginaUsuarios>(_ => throw new ServicioException("Sin conexion."));
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        var respuesta = await http.GetAsync("/inicio");
        var html = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Sin conexion.");
        html.ShouldContain("Nuevo usuario");
    }
}
