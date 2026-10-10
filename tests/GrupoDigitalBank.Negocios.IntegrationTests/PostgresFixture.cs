using Npgsql;
using Testcontainers.PostgreSql;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>
/// PostgreSQL real en un contenedor efimero con los scripts de db/ aplicados en orden.
/// Requiere Docker. Comparte un solo contenedor por clase de prueba.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();
        DataSource = NpgsqlDataSource.Create(_contenedor.GetConnectionString());

        // GetFiles no admite rangos como [0-9]*; se filtra por nombre que empiece con digito (001_..., 002_...)
        var scripts = Directory.GetFiles(RutaDelRepositorio("db"), "*.sql")
            .Where(ruta => char.IsDigit(Path.GetFileName(ruta)[0]))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (scripts.Count == 0) throw new InvalidOperationException("No se encontraron scripts numerados en db/.");

        foreach (var script in scripts)
        {
            await using var comando = DataSource.CreateCommand(await File.ReadAllTextAsync(script));
            await comando.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null) await DataSource.DisposeAsync();
        await _contenedor.DisposeAsync();
    }

    /// <summary>Sube desde la carpeta de salida hasta encontrar la solucion y devuelve la subcarpeta indicada.</summary>
    private static string RutaDelRepositorio(string subcarpeta)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GrupoDigitalBank.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("No se encontro GrupoDigitalBank.slnx."), subcarpeta);
    }
}
