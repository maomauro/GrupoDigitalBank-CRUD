using System.Net;
using System.Text.RegularExpressions;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// Hallazgo H6: las claves de Data Protection (antiforgery, cookies de TempData) deben sobrevivir a un
/// redespliegue. Cada WebApplicationFactory hace de "un despliegue" distinto: un formulario abierto en el
/// primero debe seguir siendo valido en el segundo si ambos comparten el directorio de claves.
/// </summary>
public sealed partial class ClavesProteccionTests : IDisposable
{
    private readonly List<string> _directorios = [];

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    [GeneratedRegex(@"(\.AspNetCore\.Antiforgery\.[^=]+=[^;]+)")]
    private static partial Regex CookieAntiforgery();

    private string NuevoDirectorio()
    {
        // No se crea: la aplicacion debe crearlo si no existe
        var ruta = Path.Combine(Path.GetTempPath(), "gdb-claves-" + Guid.NewGuid().ToString("N"));
        _directorios.Add(ruta);
        return ruta;
    }

    private static WebApplicationFactory<Program> Despliegue(string rutaClaves)
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.AgregarAsync(Arg.Any<UsuarioDto>()).Returns(new UsuarioDto { Id = 1, Nombre = "Ana Perez" });

        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("DataProtection:RutaClaves", rutaClaves);
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IUsuarioClient>();
                s.AddSingleton(cliente);
            });
        });
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> despliegue) =>
        despliegue.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<(string Token, string Cookie)> AbrirFormulario(HttpClient http)
    {
        var respuesta = await http.GetAsync("/Usuarios/Registrar");
        var html = await respuesta.Content.ReadAsStringAsync();
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Select(c => CookieAntiforgery().Match(c)).First(m => m.Success).Value;
        return (TokenAntiforgery().Match(html).Groups[1].Value, cookie);
    }

    private static async Task<HttpStatusCode> EnviarFormulario(HttpClient http, string token, string cookie)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/Usuarios/Registrar")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Nombre"] = "Ana Perez",
                ["FechaNacimiento"] = "1990-05-20",
                ["Sexo"] = "F",
                ["__RequestVerificationToken"] = token
            })
        };
        peticion.Headers.Add("Cookie", cookie);
        return (await http.SendAsync(peticion)).StatusCode;
    }

    [Fact]
    public async Task LasClavesSePersistenEnElDirectorioConfigurado()
    {
        var directorio = NuevoDirectorio();
        await using var despliegue = Despliegue(directorio);
        using var http = Cliente(despliegue);

        _ = await AbrirFormulario(http);   // generar un token fuerza la creacion de la clave

        Directory.Exists(directorio).ShouldBeTrue("la aplicacion debe crear el directorio de claves");
        Directory.GetFiles(directorio, "key-*.xml").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UnFormularioAbiertoAntesDeUnRedespliegueSigueSiendoValido()
    {
        var directorio = NuevoDirectorio();
        string token, cookie;
        await using (var primero = Despliegue(directorio))
        {
            using var http = Cliente(primero);
            (token, cookie) = await AbrirFormulario(http);
        }   // el primer despliegue termina por completo

        await using var segundo = Despliegue(directorio);
        using var http2 = Cliente(segundo);
        var estado = await EnviarFormulario(http2, token, cookie);

        estado.ShouldBe(HttpStatusCode.Redirect, "el formulario debe aceptarse y redirigir a la grilla");
    }

    [Fact]
    public async Task ControlConClavesDistintasElFormularioSeRechaza()
    {
        // Demuestra que las pruebas discriminan: si el segundo despliegue NO comparte claves, el token falla
        string token, cookie;
        await using (var primero = Despliegue(NuevoDirectorio()))
        {
            using var http = Cliente(primero);
            (token, cookie) = await AbrirFormulario(http);
        }

        await using var segundo = Despliegue(NuevoDirectorio());
        using var http2 = Cliente(segundo);
        var estado = await EnviarFormulario(http2, token, cookie);

        estado.ShouldBe(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        foreach (var directorio in _directorios.Where(Directory.Exists))
        {
            try { Directory.Delete(directorio, recursive: true); }
            catch (IOException) { /* limpieza de mejor esfuerzo */ }
        }
    }
}
