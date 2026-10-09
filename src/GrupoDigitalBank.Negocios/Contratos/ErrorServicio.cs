using System.Runtime.Serialization;

namespace GrupoDigitalBank.Negocios.Contratos;

/// <summary>Detalle del fault SOAP devuelto ante errores de validacion o de negocio.</summary>
[DataContract(Namespace = Constantes.Namespace)]
public class ErrorServicio
{
    [DataMember(Order = 0)] public string Codigo { get; set; } = string.Empty;
    [DataMember(Order = 1)] public string Mensaje { get; set; } = string.Empty;
    [DataMember(Order = 2)] public List<string> Detalles { get; set; } = new();
}
