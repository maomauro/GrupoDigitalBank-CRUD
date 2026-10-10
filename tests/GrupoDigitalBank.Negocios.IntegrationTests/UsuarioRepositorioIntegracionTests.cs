using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;

namespace GrupoDigitalBank.Negocios.IntegrationTests;

/// <summary>Prueba de arranque contra PostgreSQL real. Los casos completos de H7 se añaden en su issue.</summary>
[Trait("Categoria", "Integracion")]
public class UsuarioRepositorioIntegracionTests(PostgresFixture bd) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CrearConsultarYEliminarUnUsuario()
    {
        var repositorio = new UsuarioRepositorio(bd.DataSource);

        var creado = await repositorio.CrearAsync(new UsuarioDto
        {
            Nombre = "Ana Perez",
            FechaNacimiento = new DateTime(1990, 5, 20),
            Sexo = "F"
        });

        creado.Id.ShouldBeGreaterThan(0);
        var encontrado = (await repositorio.ConsultarAsync(creado.Id)).ShouldHaveSingleItem();
        encontrado.Nombre.ShouldBe("Ana Perez");
        encontrado.Sexo.ShouldBe("F");

        (await repositorio.EliminarAsync(creado.Id)).ShouldBeTrue();
        (await repositorio.ConsultarAsync(creado.Id)).ShouldBeEmpty();
    }
}
