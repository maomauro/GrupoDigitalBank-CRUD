using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Validacion;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>
/// El validador de negocios es la fuente de verdad de las reglas (la presentacion y los CHECK de la BD son
/// redes adicionales). Cubre cada regla en sus limites.
/// </summary>
public class UsuarioValidadorTests
{
    private const string MensajeNombreObligatorio = "El nombre es obligatorio.";
    private const string MensajeNombreLargo = "El nombre no puede superar 100 caracteres.";
    private const string MensajeFecha = "La fecha de nacimiento debe estar entre 1900-01-01 y hoy.";
    private const string MensajeSexo = "El sexo debe ser 'M' o 'F'.";

    private static UsuarioDto UsuarioValido() => new()
    {
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    [Fact]
    public void UsuarioValidoNoGeneraErrores()
    {
        UsuarioValidador.Validar(UsuarioValido()).ShouldBeEmpty();
    }

    [Fact]
    public void UsuarioNuloGeneraUnSoloError()
    {
        UsuarioValidador.Validar(null).ShouldBe(["El usuario es obligatorio."]);
    }

    // ---------- Nombre ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void NombreVacioONuloGeneraError(string? nombre)
    {
        var usuario = UsuarioValido();
        usuario.Nombre = nombre!;

        UsuarioValidador.Validar(usuario).ShouldBe([MensajeNombreObligatorio]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(100)]
    public void NombreDentroDelLimiteEsValido(int largo)
    {
        var usuario = UsuarioValido();
        usuario.Nombre = new string('a', largo);

        UsuarioValidador.Validar(usuario).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    public void NombreMasLargoQueElLimiteGeneraError(int largo)
    {
        var usuario = UsuarioValido();
        usuario.Nombre = new string('a', largo);

        UsuarioValidador.Validar(usuario).ShouldBe([MensajeNombreLargo]);
    }

    [Fact]
    public void ElLimiteDeLargoSeMideSobreElNombreRecortado()
    {
        var usuario = UsuarioValido();
        usuario.Nombre = "  " + new string('a', 100) + "  ";

        UsuarioValidador.Validar(usuario).ShouldBeEmpty("los espacios de los extremos no cuentan");
    }

    // ---------- Fecha de nacimiento ----------

    public static TheoryData<DateTime> FechasValidas => new()
    {
        new DateTime(1900, 1, 1),
        new DateTime(1900, 1, 2),
        new DateTime(2000, 2, 29),
        DateTime.Today.AddDays(-1),
        DateTime.Today,
        DateTime.Today.AddHours(23).AddMinutes(59)   // la hora no cuenta: se compara solo la fecha
    };

    [Theory]
    [MemberData(nameof(FechasValidas))]
    public void FechaDentroDelRangoEsValida(DateTime fecha)
    {
        var usuario = UsuarioValido();
        usuario.FechaNacimiento = fecha;

        UsuarioValidador.Validar(usuario).ShouldBeEmpty();
    }

    public static TheoryData<DateTime> FechasInvalidas => new()
    {
        default,                           // 0001-01-01: lo que llega si el formulario no envia fecha
        new DateTime(1899, 12, 31),
        new DateTime(1850, 6, 15),
        DateTime.Today.AddDays(1),
        DateTime.Today.AddYears(1)
    };

    [Theory]
    [MemberData(nameof(FechasInvalidas))]
    public void FechaFueraDelRangoGeneraError(DateTime fecha)
    {
        var usuario = UsuarioValido();
        usuario.FechaNacimiento = fecha;

        UsuarioValidador.Validar(usuario).ShouldBe([MensajeFecha]);
    }

    // ---------- Sexo ----------

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void SexoMOFEsValido(string sexo)
    {
        var usuario = UsuarioValido();
        usuario.Sexo = sexo;

        UsuarioValidador.Validar(usuario).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("m")]
    [InlineData("f")]
    [InlineData("X")]
    [InlineData("")]
    [InlineData(" M")]
    [InlineData("MF")]
    [InlineData(null)]
    public void CualquierOtroSexoGeneraError(string? sexo)
    {
        var usuario = UsuarioValido();
        usuario.Sexo = sexo!;

        UsuarioValidador.Validar(usuario).ShouldBe([MensajeSexo]);
    }

    // ---------- Combinaciones ----------

    [Fact]
    public void LosErroresSeAcumulanEnElOrdenNombreFechaSexo()
    {
        var usuario = new UsuarioDto { Nombre = "", FechaNacimiento = DateTime.Today.AddDays(1), Sexo = "X" };

        UsuarioValidador.Validar(usuario).ShouldBe([MensajeNombreObligatorio, MensajeFecha, MensajeSexo]);
    }

    [Fact]
    public void ElIdNoInfluyeEnLaValidacionDeLosDatos()
    {
        var usuario = UsuarioValido();
        usuario.Id = -999;

        UsuarioValidador.Validar(usuario).ShouldBeEmpty();
    }
}
