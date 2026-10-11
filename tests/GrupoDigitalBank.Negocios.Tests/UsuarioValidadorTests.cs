using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Validacion;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>
/// El validador de negocios es la fuente de verdad de las reglas (la presentacion y los CHECK de la BD son
/// redes adicionales). Cubre cada regla en sus limites. "Hoy" llega como parametro (la zona horaria de negocio
/// la resuelve el servicio, hallazgo B6), asi que estas pruebas son deterministas y no dependen del reloj
/// ni de la zona del equipo.
/// </summary>
public class UsuarioValidadorTests
{
    private const string MensajeNombreObligatorio = "El nombre es obligatorio.";
    private const string MensajeNombreLargo = "El nombre no puede superar 100 caracteres.";
    private const string MensajeFecha = "La fecha de nacimiento debe estar entre 1900-01-01 y hoy.";
    private const string MensajeSexo = "El sexo debe ser 'M' o 'F'.";

    private static readonly DateOnly Hoy = new(2026, 10, 10);
    private static readonly DateTime HoyComoFecha = Hoy.ToDateTime(TimeOnly.MinValue);

    private static UsuarioDto UsuarioValido() => new()
    {
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    private static List<string> Validar(UsuarioDto? usuario) => UsuarioValidador.Validar(usuario, Hoy);

    [Fact]
    public void UsuarioValidoNoGeneraErrores()
    {
        Validar(UsuarioValido()).ShouldBeEmpty();
    }

    [Fact]
    public void UsuarioNuloGeneraUnSoloError()
    {
        Validar(null).ShouldBe(["El usuario es obligatorio."]);
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

        Validar(usuario).ShouldBe([MensajeNombreObligatorio]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(100)]
    public void NombreDentroDelLimiteEsValido(int largo)
    {
        var usuario = UsuarioValido();
        usuario.Nombre = new string('a', largo);

        Validar(usuario).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    public void NombreMasLargoQueElLimiteGeneraError(int largo)
    {
        var usuario = UsuarioValido();
        usuario.Nombre = new string('a', largo);

        Validar(usuario).ShouldBe([MensajeNombreLargo]);
    }

    [Fact]
    public void ElLimiteDeLargoSeMideSobreElNombreRecortado()
    {
        var usuario = UsuarioValido();
        usuario.Nombre = "  " + new string('a', 100) + "  ";

        Validar(usuario).ShouldBeEmpty("los espacios de los extremos no cuentan");
    }

    // ---------- Fecha de nacimiento ----------

    public static TheoryData<DateTime> FechasValidas => new()
    {
        new DateTime(1900, 1, 1),
        new DateTime(1900, 1, 2),
        new DateTime(2000, 2, 29),
        HoyComoFecha.AddDays(-1),
        HoyComoFecha,
        HoyComoFecha.AddHours(23).AddMinutes(59)   // la hora no cuenta: se compara solo la fecha
    };

    [Theory]
    [MemberData(nameof(FechasValidas))]
    public void FechaDentroDelRangoEsValida(DateTime fecha)
    {
        var usuario = UsuarioValido();
        usuario.FechaNacimiento = fecha;

        Validar(usuario).ShouldBeEmpty();
    }

    public static TheoryData<DateTime> FechasInvalidas => new()
    {
        default,                           // 0001-01-01: lo que llega si el formulario no envia fecha
        new DateTime(1899, 12, 31),
        new DateTime(1850, 6, 15),
        HoyComoFecha.AddDays(1),
        HoyComoFecha.AddYears(1)
    };

    [Theory]
    [MemberData(nameof(FechasInvalidas))]
    public void FechaFueraDelRangoGeneraError(DateTime fecha)
    {
        var usuario = UsuarioValido();
        usuario.FechaNacimiento = fecha;

        Validar(usuario).ShouldBe([MensajeFecha]);
    }

    [Fact]
    public void ElLimiteSuperiorEsElHoyRecibidoNoElDelReloj()
    {
        var usuario = UsuarioValido();
        usuario.FechaNacimiento = new DateTime(2030, 1, 1);

        UsuarioValidador.Validar(usuario, new DateOnly(2030, 1, 1)).ShouldBeEmpty("ese dia es 'hoy' para quien llama");
        UsuarioValidador.Validar(usuario, new DateOnly(2029, 12, 31)).ShouldBe([MensajeFecha], "ese dia es 'manana'");
    }

    // ---------- Sexo ----------

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void SexoMOFEsValido(string sexo)
    {
        var usuario = UsuarioValido();
        usuario.Sexo = sexo;

        Validar(usuario).ShouldBeEmpty();
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

        Validar(usuario).ShouldBe([MensajeSexo]);
    }

    // ---------- Combinaciones ----------

    [Fact]
    public void LosErroresSeAcumulanEnElOrdenNombreFechaSexo()
    {
        var usuario = new UsuarioDto { Nombre = "", FechaNacimiento = HoyComoFecha.AddDays(1), Sexo = "X" };

        Validar(usuario).ShouldBe([MensajeNombreObligatorio, MensajeFecha, MensajeSexo]);
    }

    [Fact]
    public void ElIdNoInfluyeEnLaValidacionDeLosDatos()
    {
        var usuario = UsuarioValido();
        usuario.Id = -999;

        Validar(usuario).ShouldBeEmpty();
    }
}
