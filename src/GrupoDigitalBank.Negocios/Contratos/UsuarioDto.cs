using System.Runtime.Serialization;

namespace GrupoDigitalBank.Negocios.Contratos;

[DataContract(Namespace = Constantes.Namespace)]
public class UsuarioDto
{
    [DataMember(Order = 0)] public int Id { get; set; }
    [DataMember(Order = 1)] public string Nombre { get; set; } = string.Empty;
    [DataMember(Order = 2)] public DateTime FechaNacimiento { get; set; }
    /// <summary>"M" o "F".</summary>
    [DataMember(Order = 3)] public string Sexo { get; set; } = string.Empty;
}
