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
    /// <summary>Instante del alta, ya en la hora de la zona de negocio (America/Bogota).</summary>
    [DataMember(Order = 4)] public DateTime FechaRegistro { get; set; }
    /// <summary>Anos cumplidos a hoy (fecha de America/Bogota); lo calcula Negocios al responder.</summary>
    [DataMember(Order = 5)] public int Edad { get; set; }
}
