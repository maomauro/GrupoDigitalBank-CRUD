using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>
/// Arranque del host de negocios. Hallazgo B5: sin cadena de conexion no debe arrancar (y el repositorio no
/// debe traer contrasenas). Hallazgo B8: el WSDL solo se publica en desarrollo.
/// Los casos de produccion usan el entorno "Production" a proposito: en "Development" se cargan los user-secrets
/// del equipo y falsearian la prueba.
/// </summary>
public sealed class HostNegociosTests
{
    private const string CadenaValida = "Host=localhost;Port=5432;Database=pruebas;Username=app;Password=solo-para-pruebas";
    private const string RutaWsdl = "/UsuarioService.svc?wsdl";

    private static WebApplicationFactory<Program> Host(string entorno, string? cadena)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(entorno);
            // CoreWCF construye sus dispatchers a partir de la direccion base del servidor; el TestServer
            // no publica ninguna por si mismo y sin esto falla con "Could not find a base address"
            b.UseUrls("http://localhost");
            if (cadena is not null) b.UseSetting("ConnectionStrings:Postgres", cadena);
        });

    // ---------- B5: sin secretos en el repositorio, y fallar de forma clara ----------

    [Fact]
    public void ElAppsettingsNoContieneContrasenas()
    {
        var ruta = Path.Combine(RaizDelRepositorio(), "src", "GrupoDigitalBank.Negocios", "appsettings.json");

        var contenido = File.ReadAllText(ruta);

        contenido.ShouldNotContain("Password", Case.Insensitive);
        contenido.ShouldNotContain("Pwd=", Case.Insensitive);
    }

    [Fact]
    public void SinCadenaDeConexionElHostNoArrancaYExplicaQueFalta()
    {
        using var host = Host("Production", cadena: null);

        var ex = Should.Throw<InvalidOperationException>(() => host.CreateClient());

        ex.Message.ShouldContain("ConnectionStrings:Postgres");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UnaCadenaVaciaTambienSeRechazaConElMismoMensaje(string cadena)
    {
        using var host = Host("Production", cadena);

        var ex = Should.Throw<InvalidOperationException>(() => host.CreateClient());

        ex.Message.ShouldContain("ConnectionStrings:Postgres");
    }

    [Fact]
    public async Task ConUnaCadenaValidaArrancaYResponderHealth()
    {
        using var host = Host("Production", CadenaValida);
        using var http = host.CreateClient();

        var respuesta = await http.GetAsync("/health");

        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldBe("ok");
    }

    // ---------- B8: el WSDL solo en desarrollo ----------

    [Fact]
    public async Task EnDesarrolloSePublicaElWsdl()
    {
        using var host = Host("Development", CadenaValida);
        using var http = host.CreateClient();

        var respuesta = await http.GetAsync(RutaWsdl);

        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldContain("definitions");
    }

    [Theory]
    [InlineData("Production", "/UsuarioService.svc?wsdl")]
    [InlineData("Production", "/UsuarioService.svc?singleWsdl")]
    [InlineData("Production", "/UsuarioService.svc?xsd=xsd0")]
    [InlineData("Staging", "/UsuarioService.svc?wsdl")]
    public async Task FueraDeDesarrolloElContratoNoSePublicaPorNingunaVia(string entorno, string ruta)
    {
        using var host = Host(entorno, CadenaValida);
        using var http = host.CreateClient();

        var cuerpo = await (await http.GetAsync(ruta)).Content.ReadAsStringAsync();

        // Con la metadata deshabilitada CoreWCF responde una pagina de ayuda generica (HTTP 200): lo que
        // importa es que ni el WSDL ni los nombres del contrato salgan en la respuesta.
        cuerpo.ShouldNotContain("definitions", Case.Insensitive);
        cuerpo.ShouldNotContain("schema", Case.Insensitive);
        cuerpo.ShouldNotContain("UsuarioDto");
        cuerpo.ShouldNotContain("FechaNacimiento");
        cuerpo.ShouldNotContain("Agregar");
    }

    [Theory]
    [InlineData("/UsuarioService.svc?wsdl", "Agregar")]               // las operaciones; los tipos van en el esquema importado
    [InlineData("/UsuarioService.svc?singleWsdl", "FechaNacimiento")]  // todo el contrato en un solo documento
    public async Task EnDesarrolloElContratoSiSePublica(string ruta, string textoEsperado)
    {
        using var host = Host("Development", CadenaValida);
        using var http = host.CreateClient();

        var cuerpo = await (await http.GetAsync(ruta)).Content.ReadAsStringAsync();

        cuerpo.ShouldContain(textoEsperado);
    }

    private static string RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GrupoDigitalBank.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontro GrupoDigitalBank.slnx.");
    }
}
