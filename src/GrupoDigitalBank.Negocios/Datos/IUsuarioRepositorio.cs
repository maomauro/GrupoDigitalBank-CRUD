using GrupoDigitalBank.Negocios.Contratos;

namespace GrupoDigitalBank.Negocios.Datos;

public interface IUsuarioRepositorio
{
    Task<UsuarioDto> CrearAsync(UsuarioDto usuario, CancellationToken ct = default);
    Task<UsuarioDto?> ModificarAsync(UsuarioDto usuario, CancellationToken ct = default);
    Task<List<UsuarioDto>> ConsultarAsync(int? id, CancellationToken ct = default);
    Task<ResultadoPagina> ConsultarPaginaAsync(int limite, int desplazamiento, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, CancellationToken ct = default);
}
