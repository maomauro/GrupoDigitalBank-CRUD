extern alias negocios;

using System.Reflection;
using System.Runtime.Serialization;
using NegociosContratos = negocios::GrupoDigitalBank.Negocios.Contratos;
using PresentacionServicios = GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Contrato.Tests;

/// <summary>
/// Hallazgo B2 (ADR 0003): el contrato SOAP vive en Negocios (CoreWCF) y se copia a mano en Presentacion
/// (System.ServiceModel). Estas pruebas describen ambos con reflexion y exigen que coincidan en todo lo que
/// llega al cable: espacio de nombres, operaciones, nombre y tipo de cada parametro, faults, y nombre, orden y
/// tipo de cada campo. La descripcion es textual para que un fallo muestre la diferencia exacta.
/// </summary>
public sealed class ContratoDuplicadoTests
{
    [Fact]
    public void LasOperacionesDelServicioCoincidenPorNombreParametrosYFaults()
    {
        var negocios = DescribirServicio(typeof(NegociosContratos.IUsuarioService));
        var presentacion = DescribirServicio(typeof(PresentacionServicios.IUsuarioServicio));

        presentacion.ShouldBe(negocios);
    }

    [Fact]
    public void ElServicioTieneLasCincoOperacionesEsperadas()
    {
        // Evita que ambos lados se vacien a la vez y la comparacion pase en vano
        var lineas = DescribirServicio(typeof(NegociosContratos.IUsuarioService));

        lineas.Count(l => l.StartsWith("operacion ", StringComparison.Ordinal)).ShouldBe(5);
    }

    [Fact]
    public void UsuarioDtoCoincideEnNombreEspacioOrdenYTipoDeCadaCampo()
    {
        var negocios = DescribirDatos(typeof(NegociosContratos.UsuarioDto));
        var presentacion = DescribirDatos(typeof(PresentacionServicios.UsuarioDto));

        presentacion.ShouldBe(negocios);
        negocios.Count(l => l.StartsWith("campo ", StringComparison.Ordinal)).ShouldBe(6);
    }

    [Fact]
    public void PaginaUsuariosCoincideEnNombreEspacioOrdenYTipoDeCadaCampo()
    {
        var negocios = DescribirDatos(typeof(NegociosContratos.PaginaUsuarios));
        var presentacion = DescribirDatos(typeof(PresentacionServicios.PaginaUsuarios));

        presentacion.ShouldBe(negocios);
        negocios.Count(l => l.StartsWith("campo ", StringComparison.Ordinal)).ShouldBe(4);
    }

    [Fact]
    public void ErrorServicioCoincideEnNombreEspacioOrdenYTipoDeCadaCampo()
    {
        var negocios = DescribirDatos(typeof(NegociosContratos.ErrorServicio));
        var presentacion = DescribirDatos(typeof(PresentacionServicios.ErrorServicio));

        presentacion.ShouldBe(negocios);
        negocios.Count(l => l.StartsWith("campo ", StringComparison.Ordinal)).ShouldBe(3);
    }

    [Fact]
    public void LaConstanteDelEspacioDeNombresEsLaMisma()
    {
        PresentacionServicios.Constantes.Namespace.ShouldBe(NegociosContratos.Constantes.Namespace);
    }

    // ---------- descripcion textual de un contrato ----------

    private static List<string> DescribirServicio(Type contrato)
    {
        var lineas = new List<string>();
        var servicio = Atributo(contrato, "ServiceContractAttribute")
                       ?? throw new InvalidOperationException($"{contrato.Name} no tiene [ServiceContract].");
        lineas.Add($"contrato Name={Propiedad(servicio, "Name")} Namespace={Propiedad(servicio, "Namespace")}");

        foreach (var metodo in contrato.GetMethods().OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var operacion = Atributo(metodo, "OperationContractAttribute");
            if (operacion is null) continue;
            var parametros = string.Join(", ", metodo.GetParameters().Select(p => $"{Forma(p.ParameterType)} {p.Name}"));
            lineas.Add($"operacion {Forma(metodo.ReturnType)} {metodo.Name}({parametros}) " +
                       $"Name={Propiedad(operacion, "Name")} Action={Propiedad(operacion, "Action")} " +
                       $"ReplyAction={Propiedad(operacion, "ReplyAction")} OneWay={Propiedad(operacion, "IsOneWay")}");
            foreach (var fault in metodo.GetCustomAttributes().Where(a => a.GetType().Name == "FaultContractAttribute"))
            {
                var detalle = (Type)fault.GetType().GetProperty("DetailType")!.GetValue(fault)!;
                lineas.Add($"  fault {detalle.Name} Name={Propiedad(fault, "Name")} Namespace={Propiedad(fault, "Namespace")}");
            }
        }

        return lineas;
    }

    private static List<string> DescribirDatos(Type tipo)
    {
        var contrato = tipo.GetCustomAttribute<DataContractAttribute>()
                       ?? throw new InvalidOperationException($"{tipo.Name} no tiene [DataContract].");
        var lineas = new List<string> { $"datos Name={contrato.Name ?? tipo.Name} Namespace={contrato.Namespace}" };

        var campos = tipo.GetProperties()
            .Select(p => (Propiedad: p, Miembro: p.GetCustomAttribute<DataMemberAttribute>()))
            .Where(x => x.Miembro is not null)
            .OrderBy(x => x.Miembro!.Order)
            .ThenBy(x => x.Miembro!.Name ?? x.Propiedad.Name, StringComparer.Ordinal);
        foreach (var (propiedad, miembro) in campos)
        {
            lineas.Add($"campo Order={miembro!.Order} Name={miembro.Name ?? propiedad.Name} Tipo={Forma(propiedad.PropertyType)} " +
                       $"Required={miembro.IsRequired} EmitDefault={miembro.EmitDefaultValue}");
        }

        return lineas;
    }

    // Los atributos de CoreWCF y de System.ServiceModel son tipos distintos con los mismos nombres: se leen por nombre
    private static Attribute? Atributo(MemberInfo miembro, string nombre)
        => miembro.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == nombre);

    private static string Propiedad(Attribute atributo, string nombre)
        => atributo.GetType().GetProperty(nombre)?.GetValue(atributo)?.ToString() ?? "(omitido)";

    // Forma del tipo sin su espacio de nombres: UsuarioDto de un lado y del otro son tipos distintos
    private static string Forma(Type tipo)
    {
        if (Nullable.GetUnderlyingType(tipo) is { } subyacente) return Forma(subyacente) + "?";
        if (!tipo.IsGenericType) return tipo.Name;
        var nombre = tipo.Name[..tipo.Name.IndexOf('`', StringComparison.Ordinal)];
        return $"{nombre}<{string.Join(", ", tipo.GetGenericArguments().Select(Forma))}>";
    }
}
