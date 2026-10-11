using System.ServiceModel;

namespace GrupoDigitalBank.Presentacion.Servicios;

/// <summary>Cliente SOAP (BasicHttpBinding) hacia el servicio CoreWCF de la capa de negocios.</summary>
public sealed class UsuarioClient : IUsuarioClient, IDisposable
{
    private readonly ChannelFactory<IUsuarioServicio> _fabrica;

    public UsuarioClient(IConfiguration configuracion)
    {
        var url = configuracion["Negocios:Url"]
                  ?? throw new InvalidOperationException("Falta Negocios:Url.");
        var modo = url.StartsWith("https", StringComparison.OrdinalIgnoreCase)
            ? BasicHttpSecurityMode.Transport
            : BasicHttpSecurityMode.None;
        _fabrica = new ChannelFactory<IUsuarioServicio>(new BasicHttpBinding(modo), new EndpointAddress(url));
    }

    public void Dispose()
    {
        try
        {
            _fabrica.Close();
        }
        catch (Exception ex) when (ex is CommunicationException or TimeoutException)
        {
            _fabrica.Abort();
        }
    }

    public Task<UsuarioDto> AgregarAsync(UsuarioDto usuario) => Invocar(c => c.Agregar(usuario));
    public Task<UsuarioDto> ModificarAsync(UsuarioDto usuario) => Invocar(c => c.Modificar(usuario));
    public Task<List<UsuarioDto>> ConsultarAsync(int? id = null) => Invocar(c => c.Consultar(id));

    public Task<PaginaUsuarios> ConsultarPaginaAsync(int pagina, int tamano) => Invocar(c => c.ConsultarPagina(pagina, tamano));

    public Task EliminarAsync(int id) => Invocar(async c =>
    {
        await c.Eliminar(id);
        return true;
    });

    private async Task<T> Invocar<T>(Func<IUsuarioServicio, Task<T>> operacion)
    {
        var canal = _fabrica.CreateChannel();
        var comunicacion = (IClientChannel)canal;
        try
        {
            var resultado = await operacion(canal);
            comunicacion.Close();
            return resultado;
        }
        catch (FaultException<ErrorServicio> ex)
        {
            comunicacion.Abort();
            throw new ServicioException(ex.Detail.Mensaje, ex.Detail.Detalles);
        }
        catch (Exception ex) when (ex is CommunicationException or TimeoutException)
        {
            comunicacion.Abort();
            throw new ServicioException("No fue posible comunicarse con el servicio de negocios.");
        }
        catch
        {
            comunicacion.Abort();
            throw;
        }
    }
}
