extern alias negocios;

using negocios::GrupoDigitalBank.Negocios.Contratos;
using negocios::GrupoDigitalBank.Negocios.Datos;

namespace GrupoDigitalBank.Contrato.Tests;

/// <summary>Repositorio falso: la prueba de contrato no necesita PostgreSQL, solo que el servicio real responda.</summary>
public sealed class RepositorioEnMemoria : IUsuarioRepositorio
{
    private readonly Lock _candado = new();
    private readonly List<UsuarioDto> _usuarios = [];
    private int _siguienteId = 1;

    public Task<UsuarioDto> CrearAsync(UsuarioDto usuario, CancellationToken ct = default)
    {
        lock (_candado)
        {
            var creado = Copiar(usuario, _siguienteId++);
            _usuarios.Add(creado);
            return Task.FromResult(Copiar(creado, creado.Id));
        }
    }

    public Task<UsuarioDto?> ModificarAsync(UsuarioDto usuario, CancellationToken ct = default)
    {
        lock (_candado)
        {
            var indice = _usuarios.FindIndex(u => u.Id == usuario.Id);
            if (indice < 0) return Task.FromResult<UsuarioDto?>(null);
            _usuarios[indice] = Copiar(usuario, usuario.Id);
            return Task.FromResult<UsuarioDto?>(Copiar(usuario, usuario.Id));
        }
    }

    public Task<List<UsuarioDto>> ConsultarAsync(int? id, CancellationToken ct = default)
    {
        lock (_candado)
        {
            return Task.FromResult(_usuarios.Where(u => id is null || u.Id == id).Select(u => Copiar(u, u.Id)).ToList());
        }
    }

    public Task<ResultadoPagina> ConsultarPaginaAsync(int limite, int desplazamiento, CancellationToken ct = default)
    {
        lock (_candado)
        {
            var pagina = _usuarios.OrderBy(u => u.Id).Skip(desplazamiento).Take(limite).Select(u => Copiar(u, u.Id)).ToList();
            return Task.FromResult(new ResultadoPagina(pagina, _usuarios.Count));
        }
    }

    public Task<bool> EliminarAsync(int id, CancellationToken ct = default)
    {
        lock (_candado)
        {
            return Task.FromResult(_usuarios.RemoveAll(u => u.Id == id) > 0);
        }
    }

    private static UsuarioDto Copiar(UsuarioDto u, int id)
        => new() { Id = id, Nombre = u.Nombre, FechaNacimiento = u.FechaNacimiento, Sexo = u.Sexo };
}
