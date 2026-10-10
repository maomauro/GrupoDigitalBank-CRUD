namespace GrupoDigitalBank.Negocios.Tiempo;

/// <summary>
/// Zona horaria de negocio (hallazgo B6). Reglas como "la fecha de nacimiento no puede ser futura" se evaluan con
/// la fecha de ESTA zona, no con la del servidor: en el contenedor el reloj esta en UTC y, entre las 19:00 y la
/// medianoche en Colombia, la fecha UTC ya es la de manana.
/// </summary>
public static class ZonaHorariaNegocio
{
    /// <summary>Colombia: UTC-5 todo el ano, sin horario de verano. Debe coincidir con db/003_*.sql.</summary>
    public const string PorOmision = "America/Bogota";

    /// <summary>Clave de configuracion (appsettings, variable Negocios__ZonaHoraria).</summary>
    public const string ClaveDeConfiguracion = "Negocios:ZonaHoraria";

    /// <summary>Resuelve el id IANA configurado; sin valor usa <see cref="PorOmision"/>. Falla con un mensaje claro.</summary>
    public static TimeZoneInfo Resolver(string? id)
    {
        var buscado = string.IsNullOrWhiteSpace(id) ? PorOmision : id.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(buscado);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"{ClaveDeConfiguracion}='{buscado}' no es una zona horaria valida. Use un id IANA como {PorOmision} " +
                "(en Linux se necesita el paquete tzdata).", ex);
        }
    }
}

/// <summary>
/// Reloj del sistema cuya zona "local" es la de negocio. Asi <c>GetLocalNow()</c> devuelve la hora de Colombia
/// aunque el servidor este en UTC, y en las pruebas se sustituye por <c>FakeTimeProvider</c>.
/// </summary>
public sealed class ProveedorDeTiempoConZona(TimeZoneInfo zona) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => zona;
}
