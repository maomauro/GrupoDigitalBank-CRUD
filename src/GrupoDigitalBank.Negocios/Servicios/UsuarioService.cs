using CoreWCF;
using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using GrupoDigitalBank.Negocios.Tiempo;
using GrupoDigitalBank.Negocios.Validacion;
using Npgsql;

namespace GrupoDigitalBank.Negocios.Servicios;

/// <summary>Capa de negocios: valida, delega en el repositorio y traduce errores a faults SOAP.</summary>
[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
public sealed partial class UsuarioService(IUsuarioRepositorio repositorio, ILogger<UsuarioService> logger, TimeProvider tiempo) : IUsuarioService
{
    public Task<UsuarioDto> Agregar(UsuarioDto usuario) => Ejecutar(async () =>
    {
        ValidarOFallar(usuario);
        return await repositorio.CrearAsync(usuario);
    });

    public Task<UsuarioDto> Modificar(UsuarioDto usuario) => Ejecutar(async () =>
    {
        ValidarOFallar(usuario);
        if (usuario.Id <= 0) throw Fault("VALIDACION", "El id es obligatorio.");
        return await repositorio.ModificarAsync(usuario)
               ?? throw Fault("NO_ENCONTRADO", $"No existe el usuario {usuario.Id}.");
    });

    public Task<List<UsuarioDto>> Consultar(int? id) => Ejecutar(async () =>
    {
        var lista = await repositorio.ConsultarAsync(id);
        if (id.HasValue && lista.Count == 0) throw Fault("NO_ENCONTRADO", $"No existe el usuario {id}.");
        return lista;
    });

    public Task Eliminar(int id) => Ejecutar(async () =>
    {
        if (id <= 0) throw Fault("VALIDACION", "El id es obligatorio.");
        if (!await repositorio.EliminarAsync(id)) throw Fault("NO_ENCONTRADO", $"No existe el usuario {id}.");
        return true;
    });

    private async Task<T> Ejecutar<T>(Func<Task<T>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (FaultException<ErrorServicio>)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            LogErrorBaseDatos(logger, ex);
            throw Fault("ERROR_DATOS", "No fue posible acceder a la base de datos.");
        }
        catch (Exception ex)
        {
            LogErrorNoControlado(logger, ex);
            throw Fault("ERROR_INTERNO", "Ocurrio un error inesperado.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error de base de datos")]
    private static partial void LogErrorBaseDatos(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error no controlado")]
    private static partial void LogErrorNoControlado(ILogger logger, Exception ex);

    // "Hoy" es la fecha de la zona horaria de negocio (America/Bogota), no la del servidor (UTC): hallazgo B6.
    private DateOnly Hoy => DateOnly.FromDateTime(tiempo.GetLocalNow().DateTime);

    private void ValidarOFallar(UsuarioDto usuario)
    {
        var errores = UsuarioValidador.Validar(usuario, Hoy);
        if (errores.Count > 0) throw Fault("VALIDACION", "Datos invalidos.", errores);
    }

    private static FaultException<ErrorServicio> Fault(string codigo, string mensaje, List<string>? detalles = null)
        => new(new ErrorServicio { Codigo = codigo, Mensaje = mensaje, Detalles = detalles ?? new() },
               new FaultReason(mensaje));
}
