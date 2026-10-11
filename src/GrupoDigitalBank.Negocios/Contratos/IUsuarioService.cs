using CoreWCF;

namespace GrupoDigitalBank.Negocios.Contratos;

[ServiceContract(Name = "UsuarioService", Namespace = Constantes.Namespace)]
public interface IUsuarioService
{
    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<UsuarioDto> Agregar(UsuarioDto usuario);

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<UsuarioDto> Modificar(UsuarioDto usuario);

    /// <param name="id">null = todos los usuarios; con valor = solo ese usuario.</param>
    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<List<UsuarioDto>> Consultar(int? id);

    /// <param name="pagina">Base 1. Fuera de rango devuelve la ultima pagina.</param>
    /// <param name="tamano">Usuarios por pagina, de 1 a 100.</param>
    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task<PaginaUsuarios> ConsultarPagina(int pagina, int tamano);

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task Eliminar(int id);
}
