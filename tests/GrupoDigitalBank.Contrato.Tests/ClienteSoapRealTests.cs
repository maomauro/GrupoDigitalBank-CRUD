using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.Extensions.Configuration;

namespace GrupoDigitalBank.Contrato.Tests;

/// <summary>
/// Hallazgo B2: el cliente REAL de Presentacion (con su copia manual del contrato) contra el servicio REAL de
/// Negocios por SOAP sobre un socket. Si el espacio de nombres, el nombre de una operacion o de un parametro,
/// el orden de un campo o el tipo del fault dejan de coincidir, la llamada falla aqui y no en produccion.
/// </summary>
public sealed class ClienteSoapRealTests(ServidorNegocios servidor) : IClassFixture<ServidorNegocios>
{
    private UsuarioClient NuevoCliente()
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Negocios:Url"] = servidor.Url })
            .Build());

    private static UsuarioDto Valido(string nombre = "Ana Perez", string sexo = "F")
        => new() { Nombre = nombre, FechaNacimiento = new DateTime(1990, 5, 17), Sexo = sexo };

    [Fact]
    public async Task AgregarYConsultarConservanTodosLosCampos()
    {
        using var cliente = NuevoCliente();

        var creado = await cliente.AgregarAsync(Valido("Luis Gomez", "M"));
        var leido = (await cliente.ConsultarAsync(creado.Id)).ShouldHaveSingleItem();

        creado.Id.ShouldBeGreaterThan(0);
        leido.Id.ShouldBe(creado.Id);
        leido.Nombre.ShouldBe("Luis Gomez");
        leido.FechaNacimiento.ShouldBe(new DateTime(1990, 5, 17));
        leido.Sexo.ShouldBe("M");
    }

    [Fact]
    public async Task ConsultarSinIdDevuelveTodosYConIdSoloElPedido()
    {
        using var cliente = NuevoCliente();
        var a = await cliente.AgregarAsync(Valido("Uno Uno"));
        var b = await cliente.AgregarAsync(Valido("Dos Dos"));

        var todos = await cliente.ConsultarAsync();
        var solo = await cliente.ConsultarAsync(b.Id);

        todos.Select(u => u.Id).ShouldContain(a.Id);
        todos.Select(u => u.Id).ShouldContain(b.Id);
        solo.Select(u => u.Id).ShouldBe([b.Id]);
    }

    [Fact]
    public async Task ConsultarPaginaDevuelveLaPaginaPedidaConSuTotal()
    {
        using var cliente = NuevoCliente();
        for (var i = 0; i < 12; i++) await cliente.AgregarAsync(Valido($"Paginado {i:00}"));

        var pagina = await cliente.ConsultarPaginaAsync(1, 5);

        pagina.Usuarios.Count.ShouldBe(5);
        pagina.Total.ShouldBeGreaterThanOrEqualTo(12);
        pagina.Pagina.ShouldBe(1);
        pagina.Tamano.ShouldBe(5);
        pagina.Usuarios.Select(u => u.Id).ShouldBe(pagina.Usuarios.Select(u => u.Id).Order());
        pagina.Usuarios[0].Nombre.ShouldNotBeNullOrEmpty();
        pagina.Usuarios[0].FechaNacimiento.ShouldBe(new DateTime(1990, 5, 17));
    }

    [Fact]
    public async Task ConsultarPaginaFueraDeRangoDevuelveLaUltimaYNoUnaVacia()
    {
        using var cliente = NuevoCliente();
        await cliente.AgregarAsync(Valido("Para la ultima"));
        var primera = await cliente.ConsultarPaginaAsync(1, 5);
        var ultima = (primera.Total + 4) / 5;

        var pagina = await cliente.ConsultarPaginaAsync(99_999, 5);

        pagina.Pagina.ShouldBe(ultima);
        pagina.Usuarios.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ConsultarPaginaConUnTamanoInvalidoLlegaComoFaultDeValidacion()
    {
        using var cliente = NuevoCliente();

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarPaginaAsync(1, 500));

        ex.Message.ShouldBe("Datos invalidos.");
        ex.Detalles.ShouldHaveSingleItem().ShouldContain("tamano", Case.Insensitive);
    }

    [Fact]
    public async Task ModificarCambiaLosDatos()
    {
        using var cliente = NuevoCliente();
        var creado = await cliente.AgregarAsync(Valido("Nombre Viejo"));

        creado.Nombre = "Nombre Nuevo";
        creado.Sexo = "M";
        var modificado = await cliente.ModificarAsync(creado);
        var leido = (await cliente.ConsultarAsync(creado.Id)).ShouldHaveSingleItem();

        modificado.Nombre.ShouldBe("Nombre Nuevo");
        leido.Nombre.ShouldBe("Nombre Nuevo");
        leido.Sexo.ShouldBe("M");
    }

    [Fact]
    public async Task EliminarQuitaAlUsuario()
    {
        using var cliente = NuevoCliente();
        var creado = await cliente.AgregarAsync(Valido("Para Borrar"));

        await cliente.EliminarAsync(creado.Id);

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.ConsultarAsync(creado.Id));
        ex.Message.ShouldBe($"No existe el usuario {creado.Id}.");
    }

    [Fact]
    public async Task UnErrorDeValidacionLlegaComoFaultTipadoConSusDetalles()
    {
        using var cliente = NuevoCliente();

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.AgregarAsync(Valido(nombre: "")));

        // Si ErrorServicio dejara de coincidir (nombre, espacio de nombres u orden), esto llegaria como un
        // FaultException generico y el cliente lo trataria como error inesperado
        ex.Message.ShouldBe("Datos invalidos.");
        ex.Detalles.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ModificarUnIdInexistenteDevuelveNoEncontrado()
    {
        using var cliente = NuevoCliente();
        var inexistente = Valido();
        inexistente.Id = 987654;

        var ex = await Should.ThrowAsync<ServicioException>(() => cliente.ModificarAsync(inexistente));

        ex.Message.ShouldBe("No existe el usuario 987654.");
    }
}
