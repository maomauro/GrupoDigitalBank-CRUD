using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>B7: la aritmetica de la paginacion que ve el usuario (cuantas paginas hay, si hay anterior o siguiente).</summary>
public sealed class PaginaViewModelTests
{
    private static PaginaViewModel Modelo(int pagina, int tamano, int total)
        => new() { Pagina = pagina, Tamano = tamano, Total = total };

    [Theory]
    [InlineData(0, 10, 1)]     // sin usuarios sigue habiendo una pagina (vacia)
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]    // justo una pagina llena
    [InlineData(11, 10, 2)]    // uno de mas abre la segunda
    [InlineData(25, 10, 3)]
    [InlineData(50, 25, 2)]
    [InlineData(51, 25, 3)]
    [InlineData(100, 50, 2)]
    public void TotalPaginasRedondeaHaciaArribaYNuncaBajaDeUna(int total, int tamano, int esperadas)
    {
        Modelo(1, tamano, total).TotalPaginas.ShouldBe(esperadas);
    }

    [Theory]
    [InlineData(1, 3, false, true)]
    [InlineData(2, 3, true, true)]
    [InlineData(3, 3, true, false)]
    [InlineData(1, 1, false, false)]
    public void AnteriorYSiguienteSoloExistenDondeHayAdondeIr(int pagina, int totalPaginas, bool anterior, bool siguiente)
    {
        var modelo = Modelo(pagina, 10, totalPaginas * 10);

        modelo.HayAnterior.ShouldBe(anterior);
        modelo.HaySiguiente.ShouldBe(siguiente);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(25, 25)]
    [InlineData(50, 50)]
    [InlineData(5, 10)]
    [InlineData(100, 10)]
    [InlineData(0, 10)]
    [InlineData(-25, 10)]
    public void SoloSeAceptanLosTamanosOfrecidos(int pedido, int esperado)
    {
        PaginaViewModel.NormalizarTamano(pedido).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(500, 500)]
    [InlineData(100_000, 100_000)]
    [InlineData(100_001, 100_000)]
    [InlineData(int.MaxValue, 100_000)]
    public void LaPaginaPedidaSeAcotaAlRangoQueAceptaElServicio(int pedida, int esperada)
    {
        PaginaViewModel.NormalizarPagina(pedida).ShouldBe(esperada);
    }

    [Fact]
    public void DesdeElServicioCopiaLaPaginaElTotalYConvierteLosUsuarios()
    {
        var servicio = new PaginaUsuarios
        {
            Usuarios = [new UsuarioDto { Id = 4, Nombre = "Ana", FechaNacimiento = new DateTime(1990, 5, 20), Sexo = "F" }],
            Total = 31,
            Pagina = 4,
            Tamano = 10
        };

        var modelo = PaginaViewModel.Desde(servicio);

        modelo.Pagina.ShouldBe(4);
        modelo.Tamano.ShouldBe(10);
        modelo.Total.ShouldBe(31);
        modelo.TotalPaginas.ShouldBe(4);
        var usuario = modelo.Usuarios.ShouldHaveSingleItem();
        usuario.Id.ShouldBe(4);
        usuario.SexoTexto.ShouldBe("Femenino");
    }

    [Theory]
    [InlineData(0, "0 usuarios")]
    [InlineData(1, "1 usuario")]
    [InlineData(2, "2 usuarios")]
    [InlineData(1234, "1234 usuarios")]
    public void ElResumenConcuerdaEnSingularYPlural(int total, string esperado)
    {
        Modelo(1, 10, total).TextoTotal.ShouldBe(esperado);
    }
}
