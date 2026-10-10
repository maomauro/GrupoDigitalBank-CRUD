using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Validacion;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>Prueba de arranque que verifica el cableado del proyecto. Los casos completos de H7 se añaden en su issue.</summary>
public class UsuarioValidadorTests
{
    private static UsuarioDto UsuarioValido() => new()
    {
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(1990, 5, 20),
        Sexo = "F"
    };

    [Fact]
    public void UsuarioValidoNoGeneraErrores()
    {
        var errores = UsuarioValidador.Validar(UsuarioValido());

        errores.ShouldBeEmpty();
    }

    [Fact]
    public void NombreVacioGeneraError()
    {
        var usuario = UsuarioValido();
        usuario.Nombre = "   ";

        var errores = UsuarioValidador.Validar(usuario);

        errores.ShouldContain("El nombre es obligatorio.");
    }
}
