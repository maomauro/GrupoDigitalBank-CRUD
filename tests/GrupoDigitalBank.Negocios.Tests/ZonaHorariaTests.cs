using CoreWCF;
using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using GrupoDigitalBank.Negocios.Servicios;
using GrupoDigitalBank.Negocios.Tiempo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GrupoDigitalBank.Negocios.Tests;

/// <summary>
/// Hallazgo B6: "hoy" para validar la fecha de nacimiento es la fecha de la zona de negocio (America/Bogota,
/// UTC-5 sin horario de verano), no la del servidor, que en el contenedor esta en UTC. Entre las 19:00 y la
/// medianoche en Colombia la fecha UTC ya es la de manana y antes se aceptaba una fecha futura.
/// </summary>
public class ZonaHorariaTests
{
    private static readonly TimeZoneInfo Bogota = ZonaHorariaNegocio.Resolver("America/Bogota");

    private static UsuarioService ServicioEn(DateTimeOffset instanteUtc, TimeZoneInfo zona, out IUsuarioRepositorio repositorio)
    {
        var reloj = new FakeTimeProvider(instanteUtc);
        reloj.SetLocalTimeZone(zona);
        repositorio = Substitute.For<IUsuarioRepositorio>();
        repositorio.CrearAsync(Arg.Any<UsuarioDto>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<UsuarioDto>());
        return new UsuarioService(repositorio, NullLogger<UsuarioService>.Instance, reloj);
    }

    private static UsuarioDto Nacido(int anio, int mes, int dia) => new()
    {
        Nombre = "Ana Perez",
        FechaNacimiento = new DateTime(anio, mes, dia),
        Sexo = "F"
    };

    private static async Task<bool> SeAcepta(UsuarioService servicio, UsuarioDto usuario)
    {
        try
        {
            await servicio.Agregar(usuario);
            return true;
        }
        catch (FaultException<ErrorServicio> ex) when (ex.Detail.Codigo == "VALIDACION")
        {
            return false;
        }
    }

    // ---------- El caso del hallazgo ----------

    [Fact]
    public async Task DeNocheEnColombiaLaFechaDeMananaEnUtcNoSeAcepta()
    {
        // 2026-10-10 22:00 en Bogota == 2026-10-11 03:00 UTC: en UTC ya es "11", en Colombia todavia es "10"
        var servicio = ServicioEn(new DateTimeOffset(2026, 10, 11, 3, 0, 0, TimeSpan.Zero), Bogota, out _);

        (await SeAcepta(servicio, Nacido(2026, 10, 11))).ShouldBeFalse("para Colombia esa fecha es manana");
        (await SeAcepta(servicio, Nacido(2026, 10, 10))).ShouldBeTrue("hoy en Colombia");
    }

    [Fact]
    public async Task ElMismoInstanteConElServidorEnUtcSiAceptaraLaFechaFutura()
    {
        // Demuestra que la zona importa: con la zona del servidor (UTC) la misma fecha futura pasaba (el error original)
        var servicio = ServicioEn(new DateTimeOffset(2026, 10, 11, 3, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, out _);

        (await SeAcepta(servicio, Nacido(2026, 10, 11))).ShouldBeTrue();
    }

    // ---------- El borde exacto de la medianoche de Colombia ----------

    [Theory]
    [InlineData(2026, 10, 11, 4, 59, 59, false)]   // 23:59:59 del 10 en Bogota: el 11 todavia es manana
    [InlineData(2026, 10, 11, 5, 0, 0, true)]      // 00:00:00 del 11 en Bogota: el 11 ya es hoy
    [InlineData(2026, 10, 11, 5, 0, 1, true)]
    public async Task ElCambioDeDiaOcurreALaMedianocheDeBogotaNoDeUtc(int a, int m, int d, int h, int mi, int s, bool seAceptaElOnce)
    {
        var servicio = ServicioEn(new DateTimeOffset(a, m, d, h, mi, s, TimeSpan.Zero), Bogota, out _);

        (await SeAcepta(servicio, Nacido(2026, 10, 11))).ShouldBe(seAceptaElOnce);
    }

    [Fact]
    public async Task ElCambioDeAnioTampocoSeAdelantaEnUtc()
    {
        // 2026-12-31 20:00 en Bogota == 2027-01-01 01:00 UTC: el cambio de anio tampoco se adelanta
        var servicio = ServicioEn(new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero), Bogota, out _);

        (await SeAcepta(servicio, Nacido(2027, 1, 1))).ShouldBeFalse();
        (await SeAcepta(servicio, Nacido(2026, 12, 31))).ShouldBeTrue();
    }

    [Fact]
    public async Task ElAnoBisiestoFuncionaEnElLimite()
    {
        // 2028-02-29 20:00 en Bogota == 2028-03-01 01:00 UTC
        var servicio = ServicioEn(new DateTimeOffset(2028, 3, 1, 1, 0, 0, TimeSpan.Zero), Bogota, out _);

        (await SeAcepta(servicio, Nacido(2028, 2, 29))).ShouldBeTrue();
        (await SeAcepta(servicio, Nacido(2028, 3, 1))).ShouldBeFalse();
    }

    [Fact]
    public async Task LaZonaConfiguradaMandaYNoLaDelEquipo()
    {
        // 2026-10-11 12:00 UTC: en Bogota son las 07:00 del 11, en UTC las 12:00 del 11 y en Kiritimati
        // (UTC+14) las 02:00 del 12. Mismo instante, tres "hoy" distintos: el limite sigue a la zona configurada.
        var instante = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var kiritimati = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati");

        (await SeAcepta(ServicioEn(instante, Bogota, out _), Nacido(2026, 10, 12))).ShouldBeFalse("en Bogota el 12 es manana");
        (await SeAcepta(ServicioEn(instante, TimeZoneInfo.Utc, out _), Nacido(2026, 10, 12))).ShouldBeFalse("en UTC el 12 es manana");
        (await SeAcepta(ServicioEn(instante, kiritimati, out _), Nacido(2026, 10, 12))).ShouldBeTrue("en Kiritimati ya es el 12");
        (await SeAcepta(ServicioEn(instante, Bogota, out _), Nacido(2026, 10, 11))).ShouldBeTrue("en Bogota el 11 es hoy");
    }

    [Fact]
    public async Task ModificarUsaElMismoLimiteQueAgregar()
    {
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 3, 0, 0, TimeSpan.Zero));
        reloj.SetLocalTimeZone(Bogota);
        var repositorio = Substitute.For<IUsuarioRepositorio>();
        var servicio = new UsuarioService(repositorio, NullLogger<UsuarioService>.Instance, reloj);
        var usuario = Nacido(2026, 10, 11);
        usuario.Id = 5;

        var ex = await Should.ThrowAsync<FaultException<ErrorServicio>>(() => servicio.Modificar(usuario));

        ex.Detail.Codigo.ShouldBe("VALIDACION");
        await repositorio.DidNotReceiveWithAnyArgs().ModificarAsync(default!, default);
    }

    // ---------- Resolucion de la zona configurada ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinConfiguracionSeUsaAmericaBogota(string? configurada)
    {
        ZonaHorariaNegocio.Resolver(configurada).Id.ShouldBe(Bogota.Id);
        ZonaHorariaNegocio.PorOmision.ShouldBe("America/Bogota");
    }

    [Fact]
    public void BogotaEstaSiempreEnUtcMenosCincoSinHorarioDeVerano()
    {
        Bogota.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(-5));

        // No se usa SupportsDaylightSavingTime: tambien es true si la zona tuvo horario de verano ALGUNA VEZ
        // (Colombia lo aplico en 1992-1993) y en Linux la base de datos IANA conserva esa historia, mientras que
        // Windows no. Lo que importa es la regla vigente: cada mes de 2026 a 2036 esta en UTC-5 y sin horario de verano
        for (var anio = 2026; anio <= 2036; anio++)
        {
            for (var mes = 1; mes <= 12; mes++)
            {
                var instante = new DateTimeOffset(anio, mes, 15, 12, 0, 0, TimeSpan.Zero);
                Bogota.GetUtcOffset(instante).ShouldBe(TimeSpan.FromHours(-5), $"{anio}-{mes:00}");
                Bogota.IsDaylightSavingTime(instante).ShouldBeFalse($"{anio}-{mes:00}");
            }
        }
    }

    [Fact]
    public void UnaZonaValidaSeResuelve()
    {
        ZonaHorariaNegocio.Resolver("UTC").BaseUtcOffset.ShouldBe(TimeSpan.Zero);
        ZonaHorariaNegocio.Resolver(" America/Bogota ").Id.ShouldBe(Bogota.Id);
    }

    [Theory]
    [InlineData("No/Existe")]
    [InlineData("Bogota")]
    [InlineData("UTC-5")]
    public void UnaZonaInvalidaFallaConUnMensajeQueDiceQueConfiguracionCorregir(string zona)
    {
        var ex = Should.Throw<InvalidOperationException>(() => ZonaHorariaNegocio.Resolver(zona));

        ex.Message.ShouldContain("Negocios:ZonaHoraria");
        ex.Message.ShouldContain(zona);
    }

    // ---------- El proveedor de tiempo con zona ----------

    [Fact]
    public void ElProveedorExponeLaZonaConfiguradaComoZonaLocal()
    {
        var proveedor = new ProveedorDeTiempoConZona(Bogota);

        proveedor.LocalTimeZone.ShouldBe(Bogota);
        proveedor.GetLocalNow().Offset.ShouldBe(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void ElProveedorUsaElRelojRealParaElInstante()
    {
        var proveedor = new ProveedorDeTiempoConZona(Bogota);
        var antes = DateTimeOffset.UtcNow;

        var instante = proveedor.GetUtcNow();

        instante.ShouldBeInRange(antes.AddSeconds(-1), antes.AddSeconds(5));
    }
}
