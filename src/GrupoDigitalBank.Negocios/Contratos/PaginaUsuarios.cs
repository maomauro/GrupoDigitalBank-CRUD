using System.Runtime.Serialization;

namespace GrupoDigitalBank.Negocios.Contratos;

/// <summary>Una pagina de la grilla de usuarios, ordenada por id, junto con el total para poder paginar.</summary>
[DataContract(Namespace = Constantes.Namespace)]
public class PaginaUsuarios
{
    [DataMember(Order = 0)] public List<UsuarioDto> Usuarios { get; set; } = new();

    /// <summary>Cantidad de usuarios en toda la base, no solo en esta pagina.</summary>
    [DataMember(Order = 1)] public int Total { get; set; }

    /// <summary>Pagina devuelta (base 1). Puede ser menor que la pedida: una pagina fuera de rango devuelve la ultima.</summary>
    [DataMember(Order = 2)] public int Pagina { get; set; }

    [DataMember(Order = 3)] public int Tamano { get; set; }
}
