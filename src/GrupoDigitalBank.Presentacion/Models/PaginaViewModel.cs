using GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Presentacion.Models;

/// <summary>Una pagina de la grilla de usuarios y lo necesario para dibujar su navegacion.</summary>
public sealed class PaginaViewModel
{
    public const int TamanoPorOmision = 10;

    /// <summary>Limite del servicio de negocios (<c>ConsultarPagina</c>): pedir mas daria un error de validacion.</summary>
    public const int PaginaMaxima = 100_000;

    /// <summary>Tamanos ofrecidos al usuario. Cualquier otro valor de la URL se ignora.</summary>
    public static IReadOnlyList<int> Tamanos { get; } = [10, 25, 50];

    public List<UsuarioViewModel> Usuarios { get; init; } = [];
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = TamanoPorOmision;
    public int Total { get; init; }

    public int TotalPaginas => Math.Max(1, (Total + Tamano - 1) / Tamano);
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
