using System.Runtime.Serialization;
using System.ServiceModel;

namespace GrupoDigitalBank.Presentacion.Servicios;

// Espejo del contrato publicado por la capa de negocios (mismo Name/Namespace => mismo SOAP).
public static class Constantes
{
    public const string Namespace = "http://grupodigitalbank.com/usuarios/2026";
}

[DataContract(Namespace = Constantes.Namespace)]
public class UsuarioDto
{
    [DataMember(Order = 0)] public int Id { get; set; }
    [DataMember(Order = 1)] public string Nombre { get; set; } = string.Empty;
    [DataMember(Order = 2)] public DateTime FechaNacimiento { get; set; }
    [DataMember(Order = 3)] public string Sexo { get; set; } = string.Empty;
}

[DataContract(Namespace = Constantes.Namespace)]
public class ErrorServicio
{
    [DataMember(Order = 0)] public string Codigo { get; set; } = string.Empty;
    [DataMember(Order = 1)] public string Mensaje { get; set; } = string.Empty;
    [DataMember(Order = 2)] public List<string> Detalles { get; set; } = new();
}

[ServiceContract(Name = "UsuarioService", Namespace = Constantes.Namespace)]
public interface IUsuarioServicio
{
    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<UsuarioDto> Agregar(UsuarioDto usuario);

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<UsuarioDto> Modificar(UsuarioDto usuario);

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<List<UsuarioDto>> Consultar(int? id);

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task Eliminar(int id);
}
