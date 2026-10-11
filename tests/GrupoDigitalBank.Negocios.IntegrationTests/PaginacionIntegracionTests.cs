using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using Npgsql;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>
/// B7: fn_usuario_pagina y el repositorio contra PostgreSQL real. Esta clase tiene SU PROPIO contenedor (cada clase
/// de prueba comparte uno solo dentro de si misma), asi que puede contar con exactamente 25 usuarios sembrados.
/// </summary>
[Trait("Categoria", "Integracion")]
public class PaginacionIntegracionTests(PostgresFixture bd) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const int Sembrados = 25;
    private readonly UsuarioRepositorio _repositorio = new(bd.DataSource);

    public async Task InitializeAsync()
    {
        // IAsyncLifetime se ejecuta antes de CADA prueba: se siembra solo si la base esta vacia
        await using var contar = bd.AdminDataSource.CreateCommand("SELECT count(*) FROM usuario");
        if ((long)(await contar.ExecuteScalarAsync())! > 0) return;
        for (var i = 1; i <= Sembrados; i++)
        {
            await _repositorio.CrearAsync(new UsuarioDto
            {
                Nombre = $"Usuario {i:00}",
                FechaNacimiento = new DateTime(1990, 1, 1).AddDays(i),
                Sexo = i % 2 == 0 ? "F" : "M"
            });
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LaPrimeraPaginaTraeLosPrimerosDiezPorIdYElTotalDeLaTabla()
    {
        var pagina = await _repositorio.ConsultarPaginaAsync(10, 0);

        pagina.Total.ShouldBe(Sembrados);
        pagina.Usuarios.Count.ShouldBe(10);
        pagina.Usuarios.Select(u => u.Nombre).ShouldBe(Enumerable.Range(1, 10).Select(i => $"Usuario {i:00}"));
        pagina.Usuarios.Select(u => u.Id).ShouldBe(pagina.Usuarios.Select(u => u.Id).Order());
    }

    [Fact]
    public async Task LaUltimaPaginaTraeSoloLosQueQuedan()
    {
        var pagina = await _repositorio.ConsultarPaginaAsync(10, 20);

        pagina.Usuarios.Select(u => u.Nombre).ShouldBe(Enumerable.Range(21, 5).Select(i => $"Usuario {i:00}"));
        pagina.Total.ShouldBe(Sembrados);
    }

    [Fact]
    public async Task UnaPaginaFueraDeRangoVieneVaciaPeroConElTotal()
    {
        var pagina = await _repositorio.ConsultarPaginaAsync(10, 500);

        pagina.Usuarios.ShouldBeEmpty();
        pagina.Total.ShouldBe(Sembrados);
    }

    [Fact]
    public async Task LasPaginasNoSeSolapanNiDejanHuecos()
    {
        var ids = new List<int>();
        for (var desplazamiento = 0; desplazamiento < Sembrados; desplazamiento += 7)
            ids.AddRange((await _repositorio.ConsultarPaginaAsync(7, desplazamiento)).Usuarios.Select(u => u.Id));

        ids.Count.ShouldBe(Sembrados);
        ids.Distinct().Count().ShouldBe(Sembrados);
        ids.ShouldBe(ids.Order());
    }

    [Fact]
    public async Task ModificarUnUsuarioNoCambiaSuLugarEnLaPagina()
    {
        var antes = (await _repositorio.ConsultarPaginaAsync(5, 0)).Usuarios;
        var primero = antes[0];

        UsuarioDto ConNombre(string nombre) => new() { Id = primero.Id, Nombre = nombre, FechaNacimiento = primero.FechaNacimiento, Sexo = primero.Sexo };
        await _repositorio.ModificarAsync(ConNombre("Zzz renombrado"));
        try
        {
            var despues = (await _repositorio.ConsultarPaginaAsync(5, 0)).Usuarios;

            despues.Select(u => u.Id).ShouldBe(antes.Select(u => u.Id));
            despues[0].Nombre.ShouldBe("Zzz renombrado");
        }
        finally
        {
            // La base la comparten todas las pruebas de la clase: se deja como estaba
            await _repositorio.ModificarAsync(ConNombre(primero.Nombre));
        }
    }

    [Fact]
    public async Task ConLaTablaVaciaDevuelveCeroUsuariosYTotalCero()
    {
        // Dentro de una transaccion que se revierte: las demas pruebas de la clase no ven el borrado
        await using var conexion = await bd.AdminDataSource.OpenConnectionAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();
        await using (var borrar = new NpgsqlCommand("DELETE FROM usuario", conexion, transaccion))
        {
            await borrar.ExecuteNonQueryAsync();
        }

        await using var consulta = new NpgsqlCommand("SELECT id, total FROM fn_usuario_pagina(10, 0)", conexion, transaccion);
        await using var lector = await consulta.ExecuteReaderAsync();
        (await lector.ReadAsync()).ShouldBeTrue("debe devolver una fila con el total aunque no haya usuarios");
        lector.IsDBNull(0).ShouldBeTrue();
        lector.GetInt64(1).ShouldBe(0);
        (await lector.ReadAsync()).ShouldBeFalse();

        await lector.CloseAsync();
        await transaccion.RollbackAsync();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(10, -1)]
    public async Task LaFuncionRechazaLimitesInvalidosPorSiSolaConSqlstate22023(int limite, int desplazamiento)
    {
        // Defensa en profundidad: Negocios ya valida, pero la base no confia en quien la llame
        await using var comando = bd.DataSource.CreateCommand("SELECT * FROM fn_usuario_pagina(@limite, @desplazamiento)");
        comando.Parameters.AddWithValue("limite", limite);
        comando.Parameters.AddWithValue("desplazamiento", desplazamiento);

        var error = await Should.ThrowAsync<PostgresException>(() => comando.ExecuteNonQueryAsync());

        error.SqlState.ShouldBe("22023");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task LaFuncionAceptaLosLimitesExactos(int limite)
    {
        var pagina = await _repositorio.ConsultarPaginaAsync(limite, 0);

        pagina.Usuarios.Count.ShouldBe(Math.Min(limite, Sembrados));
    }
}
