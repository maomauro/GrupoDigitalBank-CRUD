extern alias negocios;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using negocios::GrupoDigitalBank.Negocios.Datos;

namespace GrupoDigitalBank.Contrato.Tests;

/// <summary>
/// El host REAL de Negocios (CoreWCF, validador, servicio) escuchando en un puerto de Kestrel, con el
/// repositorio sustituido por uno en memoria. El cliente SOAP de Presentacion necesita un socket de verdad:
/// el TestServer en memoria no sirve para WCF.
/// </summary>
public sealed class ServidorNegocios : IAsyncLifetime
{
    private WebApplicationFactory<negocios::Program>? _fabrica;

    public RepositorioEnMemoria Repositorio { get; } = new();

    public string Url { get; private set; } = string.Empty;

    public Task InitializeAsync()
    {
        _fabrica = new WebApplicationFactory<negocios::Program>().WithWebHostBuilder(b =>
        {
            // "Production" a proposito: en "Development" se cargarian los user-secrets del equipo
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=pruebas;Username=app;Password=solo-para-pruebas");
            // Puerto libre en el bucle local, sea cual sea el entorno: ASPNETCORE_URLS o ASPNETCORE_HTTP_PORTS (la imagen
            // del SDK de Docker define esta ultima) harian escuchar en un comodin como [::]:8080, que ni sirve como destino
            // ni esta garantizado libre. "urls" tiene prioridad sobre HTTP_PORTS.
            b.UseUrls("http://127.0.0.1:0");
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IUsuarioRepositorio>();
                s.AddSingleton<IUsuarioRepositorio>(Repositorio);
            });
        });
        _fabrica.UseKestrel();
        _fabrica.StartServer();

        // La direccion real, con el puerto que asigno el sistema operativo
        var direccion = _fabrica.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Url = new Uri(new Uri(direccion), "UsuarioService.svc").ToString();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_fabrica is not null) await _fabrica.DisposeAsync();
    }
}
