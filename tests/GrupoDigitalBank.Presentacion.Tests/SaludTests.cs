using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// /health indica que el proceso web esta vivo. Debe responder sin autenticacion (el proxy lo deja
/// fuera del Basic Auth para los monitores) y SIN depender del servicio de negocios.
/// </summary>
public sealed class SaludTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthResponde200ConOk()
    {
        using var http = fabrica.CreateClient();

        var respuesta = await http.GetAsync("/health");

        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        (await respuesta.Content.ReadAsStringAsync()).ShouldBe("ok");
    }

    [Fact]
    public async Task HealthNoDependeDelServicioDeNegocios()
    {
        var cliente = Substitute.For<IUsuarioClient>();
        cliente.ConsultarAsync(Arg.Any<int?>()).Returns<List<UsuarioDto>>(_ => throw new ServicioException("caido"));
        using var http = fabrica.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IUsuarioClient>();
            s.AddSingleton(cliente);
        })).CreateClient();

        var respuesta = await http.GetAsync("/health");

        respuesta.IsSuccessStatusCode.ShouldBeTrue();
        await cliente.DidNotReceiveWithAnyArgs().ConsultarAsync(default);
        await cliente.DidNotReceiveWithAnyArgs().ConsultarPaginaAsync(default, default);
    }
}
