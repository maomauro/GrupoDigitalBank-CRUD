using System.Net;
using System.Net.Sockets;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.Extensions.Configuration;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>
/// Cliente SOAP. Aqui se prueba su comportamiento ante fallos de comunicacion (un servicio caido debe
/// convertirse en un error amigable, nunca en una excepcion de WCF); el camino feliz contra un servidor
/// CoreWCF real, y el contrato duplicado, pertenecen al hallazgo B2.
/// </summary>
public sealed class UsuarioClientTests(UsuarioClientTests.ServidorQueCierraConexiones servidor)
    : IClassFixture<UsuarioClientTests.ServidorQueCierraConexiones>
{
    private string UrlCaida => servidor.Url;

    private static IConfiguration Config(string? url) =>
        new ConfigurationBuilder().AddInMemoryCollection(url is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["Negocios:Url"] = url }).Build();

    [Fact]
    public void SinLaUrlDeNegociosFallaAlCrearseConUnMensajeClaro()
    {
        var ex = Should.Throw<InvalidOperationException>(() => new UsuarioClient(Config(null)));

        ex.Message.ShouldContain("Negocios:Url");
    }

    [Theory]
    [InlineData("http://localhost:5081/UsuarioService.svc")]
    [InlineData("https://negocios.interno/UsuarioService.svc")]
    public void SeCreaConUrlHttpOHttps(string url)
    {
        using var cliente = new UsuarioClient(Config(url));

        cliente.ShouldNotBeNull();
    }

    [Fact]
    public async Task ConsultarConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarAsync());

        ex.Message.ShouldBe("No fue posible comunicarse con el servicio de negocios.");
        ex.Detalles.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsultarPaginaConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarPaginaAsync(1, 10));

        ex.Message.ShouldBe("No fue posible comunicarse con el servicio de negocios.");
    }

    [Fact]
    public async Task ConsultarPorIdConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarAsync(3));
    }

    [Fact]
    public async Task AgregarConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.AgregarAsync(new UsuarioDto { Nombre = "Ana" }));

        ex.Message.ShouldContain("No fue posible comunicarse");
    }

    [Fact]
    public async Task ModificarConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        await Should.ThrowAsync<ServicioException>(() => cliente.ModificarAsync(new UsuarioDto { Id = 1, Nombre = "Ana" }));
    }

    [Fact]
    public async Task EliminarConElServicioCaidoDevuelveUnErrorAmigable()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        await Should.ThrowAsync<ServicioException>(() => cliente.EliminarAsync(1));
    }

    [Fact]
    public async Task UnaFallaDeComunicacionNoInutilizaAlClienteParaLasSiguientesLlamadas()
    {
        using var cliente = new UsuarioClient(Config(UrlCaida));

        await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarAsync());
        await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarAsync());   // sigue siendo un error amigable, no ObjectDisposed
    }

    [Fact]
    public void DisponerVariasVecesNoLanzaExcepciones()
    {
        var cliente = new UsuarioClient(Config("http://localhost:5081/UsuarioService.svc"));

        Should.NotThrow(() =>
        {
            cliente.Dispose();
            cliente.Dispose();
        });
    }

    /// <summary>
    /// Servidor TCP local que acepta cada conexion y la cierra al instante: simula un servicio caido o que
    /// corta la comunicacion, sin esperar los reintentos de conexion de Windows (~2 s por llamada contra
    /// un puerto cerrado) y sin depender de que un puerto concreto este libre.
    /// </summary>
    public sealed class ServidorQueCierraConexiones : IDisposable
    {
        private readonly TcpListener _escucha = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _fin = new();

        public string Url { get; }

        public ServidorQueCierraConexiones()
        {
            _escucha.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_escucha.LocalEndpoint).Port}/UsuarioService.svc";
            _ = Task.Run(AceptarYCerrar);
        }

        private async Task AceptarYCerrar()
        {
            while (!_fin.IsCancellationRequested)
            {
                try
                {
                    using var conexion = await _escucha.AcceptTcpClientAsync(_fin.Token);   // al salir del using se cierra
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            _fin.Cancel();
            _escucha.Stop();
            _fin.Dispose();
        }
    }
}
