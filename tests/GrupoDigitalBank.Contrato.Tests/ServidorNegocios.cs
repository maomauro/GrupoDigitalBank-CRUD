extern alias negocios;

using Microsoft.AspNetCore.Hosting;
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
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IUsuarioRepositorio>();
                s.AddSingleton<IUsuarioRepositorio>(Repositorio);
            });
        });
        _fabrica.UseKestrel(0);
        _fabrica.StartServer();
        Url = new Uri(_fabrica.ClientOptions.BaseAddress, "UsuarioService.svc").ToString();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_fabrica is not null) await _fabrica.DisposeAsync();
    }
}
