namespace GrupoDigitalBank.Presentacion.Servicios;

/// <summary>Error funcional/tecnico devuelto por la capa de negocios, listo para mostrar al usuario.</summary>
public sealed class ServicioException(string mensaje, IReadOnlyList<string>? detalles = null) : Exception(mensaje)
{
    public IReadOnlyList<string> Detalles { get; } = detalles ?? Array.Empty<string>();
}

public interface IUsuarioClient
{
    Task<UsuarioDto> AgregarAsync(UsuarioDto usuario);
    Task<UsuarioDto> ModificarAsync(UsuarioDto usuario);
    Task<List<UsuarioDto>> ConsultarAsync(int? id = null);
    Task<PaginaUsuarios> ConsultarPaginaAsync(int pagina, int tamano);
    Task EliminarAsync(int id);
}
