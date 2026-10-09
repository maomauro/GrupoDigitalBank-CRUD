using GrupoDigitalBank.Negocios.Contratos;
using Npgsql;
using NpgsqlTypes;

namespace GrupoDigitalBank.Negocios.Datos;

/// <summary>Acceso a datos via ADO.NET (Npgsql). Todo el CRUD pasa por fn_usuario_crud.</summary>
public sealed class UsuarioRepositorio(NpgsqlDataSource dataSource) : IUsuarioRepositorio
{
    private const string Sql =
        "SELECT id, nombre, fecha_nacimiento, sexo FROM fn_usuario_crud(@op, @id, @nombre, @fecha, @sexo)";

    public async Task<UsuarioDto> CrearAsync(UsuarioDto u, CancellationToken ct = default)
        => (await EjecutarAsync("C", null, u, ct)).Single();

    public async Task<UsuarioDto?> ModificarAsync(UsuarioDto u, CancellationToken ct = default)
        => (await EjecutarAsync("U", u.Id, u, ct)).SingleOrDefault();

    public Task<List<UsuarioDto>> ConsultarAsync(int? id, CancellationToken ct = default)
        => EjecutarAsync("R", id, null, ct);

    public async Task<bool> EliminarAsync(int id, CancellationToken ct = default)
        => (await EjecutarAsync("D", id, null, ct)).Count > 0;

    private async Task<List<UsuarioDto>> EjecutarAsync(string operacion, int? id, UsuarioDto? u, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand(Sql);
        cmd.Parameters.Add(new NpgsqlParameter("op", NpgsqlDbType.Char) { Value = operacion });
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer) { Value = (object?)id ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("nombre", NpgsqlDbType.Varchar) { Value = (object?)u?.Nombre.Trim() ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("fecha", NpgsqlDbType.Date)
            { Value = u is null ? DBNull.Value : DateOnly.FromDateTime(u.FechaNacimiento) });
        cmd.Parameters.Add(new NpgsqlParameter("sexo", NpgsqlDbType.Char) { Value = (object?)u?.Sexo ?? DBNull.Value });

        var resultado = new List<UsuarioDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            resultado.Add(new UsuarioDto
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                FechaNacimiento = reader.GetFieldValue<DateOnly>(2).ToDateTime(TimeOnly.MinValue),
                Sexo = reader.GetString(3).Trim()
            });
        }
        return resultado;
    }
}
