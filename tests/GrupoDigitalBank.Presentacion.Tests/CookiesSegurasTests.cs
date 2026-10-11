using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// ADR 0011: la aplicacion corre detras de un proxy que termina TLS y le avisa con X-Forwarded-Proto. Las cookies
/// deben llevar "Secure" cuando el visitante uso HTTPS, pero no cuando la peticion es HTTP (desarrollo local en
/// http://localhost:8080, donde una cookie Secure se descartaria y los formularios dejarian de funcionar).
/// </summary>
public sealed class CookiesSegurasTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string[]> CookiesDe(string? protocoloReenviado)
    {
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(Substitute.For<IUsuarioClient>());
        })).CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/Usuarios/Registrar");
        if (protocoloReenviado is not null) peticion.Headers.Add("X-Forwarded-Proto", protocoloReenviado);

        var respuesta = await http.SendAsync(peticion);

        return [.. respuesta.Headers.GetValues("Set-Cookie")];
    }

    private static bool EsSegura(string cookie) => cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task ConHttpsReenviadoTodasLasCookiesSalenConSecure()
    {
        var cookies = await CookiesDe("https");

        cookies.ShouldNotBeEmpty();
        cookies.ShouldAllBe(c => EsSegura(c), "ninguna cookie debe viajar por HTTP si el visitante uso HTTPS");
    }

    [Fact]
    public async Task LaCookieAntiforgeryEsSeguraConHttpsReenviado()
    {
        var cookies = await CookiesDe("https");

        EsSegura(cookies.Single(c => c.Contains("Antiforgery", StringComparison.Ordinal))).ShouldBeTrue();
    }

    [Theory]
    [InlineData("http")]
    [InlineData(null)]
    public async Task SinHttpsLasCookiesNoSonSegurasParaQueFuncioneElDesarrolloLocal(string? protocolo)
    {
        var cookies = await CookiesDe(protocolo);

        cookies.ShouldNotBeEmpty();
        cookies.ShouldAllBe(c => !EsSegura(c), "en http://localhost una cookie Secure se descartaria");
    }

    [Fact]
    public void LasCookiesDeLaAplicacionSiguenLaPoliticaDelEsquemaDeLaPeticion()
    {
        using var servidor = fabrica.WithWebHostBuilder(_ => { });

        var antiforgery = servidor.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        var tempData = servidor.Services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value;

        antiforgery.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
        tempData.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
    }
}
