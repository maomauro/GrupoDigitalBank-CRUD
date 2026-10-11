using GrupoDigitalBank.Negocios.Contratos;

namespace GrupoDigitalBank.Negocios.Datos;

/// <summary>Filas de una pagina y el total de la tabla (se conoce aunque la pagina pedida este vacia).</summary>
public sealed record ResultadoPagina(List<UsuarioDto> Usuarios, int Total);
