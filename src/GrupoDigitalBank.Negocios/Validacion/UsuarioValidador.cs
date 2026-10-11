using GrupoDigitalBank.Negocios.Contratos;

namespace GrupoDigitalBank.Negocios.Validacion;

public static class UsuarioValidador
{
    private static readonly DateTime FechaMinima = new(1900, 1, 1);

    /// <param name="hoy">La fecha de HOY en la zona horaria de negocio (la calcula el servicio, hallazgo B6).</param>
    public static List<string> Validar(UsuarioDto? usuario, DateOnly hoy)
    {
        var errores = new List<string>();
        if (usuario is null)
        {
            errores.Add("El usuario es obligatorio.");
            return errores;
        }

        var nombre = usuario.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0) errores.Add("El nombre es obligatorio.");
        else if (nombre.Length > 100) errores.Add("El nombre no puede superar 100 caracteres.");

        if (usuario.FechaNacimiento.Date < FechaMinima || usuario.FechaNacimiento.Date > hoy.ToDateTime(TimeOnly.MinValue))
            errores.Add("La fecha de nacimiento debe estar entre 1900-01-01 y hoy.");

        if (usuario.Sexo is not ("M" or "F"))
            errores.Add("El sexo debe ser 'M' o 'F'.");

        return errores;
    }
}
