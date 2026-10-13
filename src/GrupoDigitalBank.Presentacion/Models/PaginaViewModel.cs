using GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Presentacion.Models;

/// <summary>Una pagina de la grilla de usuarios y lo necesario para dibujar su navegacion.</summary>
public sealed class PaginaViewModel
{
    public const int TamanoPorOmision = 5;

    /// <summary>Limite del servicio de negocios (<c>ConsultarPagina</c>): pedir mas daria un error de validacion.</summary>
    public const int PaginaMaxima = 100_000;

    /// <summary>Tamanos ofrecidos al usuario. Cualquier otro valor de la URL se ignora.</summary>
    public static IReadOnlyList<int> Tamanos { get; } = [5, 10, 25, 50];

    public List<UsuarioViewModel> Usuarios { get; init; } = [];
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = TamanoPorOmision;
    public int Total { get; init; }

    public int TotalPaginas => Math.Max(1, (Total + Tamano - 1) / Tamano);
    public bool HayVariasPaginas => TotalPaginas > 1;

    /// <summary>
    /// Tamanos que tiene sentido ofrecer: ninguno mientras todo quepa en la primera opcion; cada opcion mayor aparece
    /// cuando los usuarios superan la anterior (5 -> con mas de 5, 10 -> con mas de 5, 25 -> con mas de 10, 50 -> con mas de 25).
    /// </summary>
    public IReadOnlyList<int> TamanosOfrecidos => Total <= Tamanos[0]
        ? []
        : Tamanos.Where((t, i) => i == 0 || Total > Tamanos[i - 1] || t == Tamano).ToList();

    public bool HayAnterior => Pagina > 1;
    public bool HaySiguiente => Pagina < TotalPaginas;
    public string TextoTotal => Total == 1 ? "1 usuario" : $"{Total} usuarios";

    public static int NormalizarTamano(int tamano) => Tamanos.Contains(tamano) ? tamano : TamanoPorOmision;

    public static int NormalizarPagina(int pagina) => Math.Clamp(pagina, 1, PaginaMaxima);

    public static PaginaViewModel Desde(PaginaUsuarios pagina) => new()
    {
        Usuarios = pagina.Usuarios.Select(UsuarioViewModel.DesdeDto).ToList(),
        Pagina = pagina.Pagina,
        Tamano = pagina.Tamano,
        Total = pagina.Total
    };
}
