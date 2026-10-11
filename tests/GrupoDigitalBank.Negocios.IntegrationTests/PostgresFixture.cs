using Npgsql;
using Testcontainers.PostgreSql;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>
/// PostgreSQL real en un contenedor efimero con los scripts de db/ aplicados en orden, igual que el
/// migrador (psql, una transaccion por script, variables app_user y app_pass). Requiere Docker.
/// Comparte un solo contenedor por clase de prueba.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string UsuarioApp = "usuarios_app";
    public const string ClaveApp = "clave-app-solo-para-pruebas";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Conexion como el rol de la aplicacion (minimo privilegio): lo que usa Negocios en produccion.</summary>
    public NpgsqlDataSource DataSource { get; private set; } = null!;

    /// <summary>Conexion como propietario/superusuario, solo para preparar y verificar permisos.</summary>
    public NpgsqlDataSource AdminDataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        // GetFiles no admite rangos como [0-9]*; se filtra por nombre que empiece con digito (001_..., 002_...)
        var scripts = Directory.GetFiles(RutaDelRepositorio("db"), "*.sql")
            .Where(ruta => char.IsDigit(Path.GetFileName(ruta)[0]))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (scripts.Count == 0) throw new InvalidOperationException("No se encontraron scripts numerados en db/.");

        foreach (var script in scripts)
        {
            var destino = "/tmp/" + Path.GetFileName(script);
            await _contenedor.CopyAsync(await File.ReadAllBytesAsync(script), destino);
            var resultado = await _contenedor.ExecAsync(
            [
                "psql", "-X", "-q", "-v", "ON_ERROR_STOP=1", "--single-transaction",
                "-U", PostgreSqlBuilder.DefaultUsername, "-d", PostgreSqlBuilder.DefaultDatabase,
                "-v", $"app_user={UsuarioApp}", "-v", $"app_pass={ClaveApp}",
                "-f", destino
            ]);
            if (resultado.ExitCode != 0)
                throw new InvalidOperationException($"Fallo al aplicar {Path.GetFileName(script)}: {resultado.Stderr}");
        }

        var admin = _contenedor.GetConnectionString();
        AdminDataSource = NpgsqlDataSource.Create(admin);
        DataSource = NpgsqlDataSource.Create(
            new NpgsqlConnectionStringBuilder(admin) { Username = UsuarioApp, Password = ClaveApp }.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null) await DataSource.DisposeAsync();
        if (AdminDataSource is not null) await AdminDataSource.DisposeAsync();
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
