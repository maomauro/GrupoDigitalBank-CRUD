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

    [OperationContract, FaultContract(typeof(ErrorServicio))]
    Task Eliminar(int id);
}
