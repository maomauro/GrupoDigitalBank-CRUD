using System.ComponentModel.DataAnnotations;
using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Presentacion.Tests;

/// <summary>Modelo de la vista: validacion de formulario (DataAnnotations) y conversion hacia/desde el contrato SOAP.</summary>
public class UsuarioViewModelTests
{
    private static UsuarioViewModel Valido() => new()
    {
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    private static List<string> Errores(UsuarioViewModel modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados.Select(r => r.ErrorMessage!).ToList();
    }

    // ---------- DataAnnotations ----------

    [Fact]
    public void UnModeloValidoNoTieneErrores()
    {
        Errores(Valido()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ElNombreEsObligatorio(string? nombre)
    {
        var modelo = Valido();
        modelo.Nombre = nombre!;

        Errores(modelo).ShouldBe(["El nombre es obligatorio."]);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(101, 1)]
    public void ElNombreNoPuedeSuperar100Caracteres(int largo, int errores)
    {
        var modelo = Valido();
        modelo.Nombre = new string('a', largo);

        Errores(modelo).Count.ShouldBe(errores);
    }

    [Fact]
    public void LaFechaDeNacimientoEsObligatoria()
    {
        var modelo = Valido();
        modelo.FechaNacimiento = null;

        Errores(modelo).ShouldBe(["La fecha de nacimiento es obligatoria."]);
    }

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void ElSexoMOFEsValido(string sexo)
    {
        var modelo = Valido();
        modelo.Sexo = sexo;

        Errores(modelo).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("X")]
    [InlineData("m")]
    [InlineData("MF")]
    [InlineData("")]
    public void UnSexoDistintoDeMOFEsInvalido(string sexo)
    {
        var modelo = Valido();
        modelo.Sexo = sexo;

        Errores(modelo).ShouldNotBeEmpty();
    }

    [Fact]
    public void ElSexoEsObligatorio()
    {
        var modelo = Valido();
        modelo.Sexo = null;

        Errores(modelo).ShouldBe(["El sexo es obligatorio."]);
    }

    // ---------- Conversion ----------

    [Fact]
    public void ADtoRecortaElNombreYCopiaLosCampos()
    {
        var modelo = Valido();
        modelo.Id = 4;
        modelo.Nombre = "  Ana Perez  ";

        var dto = modelo.ADto();

        dto.Id.ShouldBe(4);
        dto.Nombre.ShouldBe("Ana Perez");
        dto.FechaNacimiento.ShouldBe(new DateTime(1990, 5, 20));
        dto.Sexo.ShouldBe("F");
    }

    [Fact]
    public void ADtoConValoresNulosUsaValoresPorOmisionEnLugarDeFallar()
    {
        var modelo = new UsuarioViewModel { Nombre = "x", FechaNacimiento = null, Sexo = null };

        var dto = modelo.ADto();

        dto.FechaNacimiento.ShouldBe(default);
        dto.Sexo.ShouldBeEmpty();
    }

    [Fact]
    public void DesdeDtoCopiaTodosLosCampos()
    {
        var dto = new UsuarioDto { Id = 9, Nombre = "Beatriz", FechaNacimiento = new DateTime(1980, 1, 2), Sexo = "M" };

        var modelo = UsuarioViewModel.DesdeDto(dto);

        modelo.Id.ShouldBe(9);
        modelo.Nombre.ShouldBe("Beatriz");
        modelo.FechaNacimiento.ShouldBe(new DateTime(1980, 1, 2));
        modelo.Sexo.ShouldBe("M");
    }

    [Theory]
    [InlineData("M", "Masculino")]
    [InlineData("F", "Femenino")]
    [InlineData("X", "")]
    [InlineData(null, "")]
    public void SexoTextoMuestraLaDescripcion(string? sexo, string esperado)
    {
        new UsuarioViewModel { Sexo = sexo }.SexoTexto.ShouldBe(esperado);
    }

    // ---------- ServicioException ----------

    [Fact]
    public void ServicioExceptionSinDetallesTieneUnaListaVacia()
    {
        new ServicioException("x").Detalles.ShouldBeEmpty();
    }

    [Fact]
    public void ServicioExceptionConservaMensajeYDetalles()
    {
        var ex = new ServicioException("Datos invalidos.", ["a", "b"]);

        ex.Message.ShouldBe("Datos invalidos.");
        ex.Detalles.ShouldBe(["a", "b"]);
    }
}
