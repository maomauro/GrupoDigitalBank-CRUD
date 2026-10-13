using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace GrupoDigitalBank.Presentacion.Controllers;

/// <summary>
/// Inicio publico (explica como se desarrollo la solucion), cierre de sesion y, con sesion, el panel con la grilla de usuarios.
/// Las acciones publicas (Index y Salir) no consultan el servicio de negocios ni muestran datos: por eso el proxy las deja fuera
/// del Basic Auth (ADR 0016). Panel y Acerca SI quedan detras de el.
/// </summary>
public sealed class InicioController : Controller
{
    /// <summary>Realm que anuncia el Basic Auth de Caddy (su valor por omision). Tiene que coincidir para que el navegador olvide las credenciales.</summary>
    internal const string RealmBasicAuth = "restricted";

    public IActionResult Index() => View();

    /// <summary>
    /// La misma explicacion de la solucion que "/", pero CON sesion: vive en /acerca, que el proxy protege, y asi el menu
    /// muestra "Inicio" y "Cerrar sesion" en lugar de "Iniciar sesion". Es a donde lleva el titulo del menu si ya hay sesion.
    /// </summary>
    public IActionResult Acerca() => View();

    /// <summary>
    /// Inicio con sesion: la grilla de usuarios con el boton de nuevo usuario y, debajo, la explicacion de la solucion.
    /// Vive en /inicio, que el proxy SI protege con usuario y clave (solo "/" y "/salir" son publicas). El cliente SOAP llega
    /// por [FromServices] para que las acciones publicas no dependan del servicio de negocios.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Panel([FromServices] IUsuarioClient cliente, int pagina = 1, int tamano = PaginaViewModel.TamanoPorOmision)
    {
        // Lo que llega por la URL no es de fiar: se acota a lo que el servicio acepta en vez de mostrar un error
        pagina = PaginaViewModel.NormalizarPagina(pagina);
        tamano = PaginaViewModel.NormalizarTamano(tamano);
        try
        {
            return View(PaginaViewModel.Desde(await cliente.ConsultarPaginaAsync(pagina, tamano)));
        }
        catch (ServicioException ex)
        {
            if (ex.Detalles.Count == 0) ModelState.AddModelError(string.Empty, ex.Message);
            foreach (var detalle in ex.Detalles) ModelState.AddModelError(string.Empty, detalle);
            return View(new PaginaViewModel { Pagina = pagina, Tamano = tamano });
        }
    }

    /// <summary>
    /// Basic Auth no tiene sesion que cerrar: el navegador reenvia las credenciales guardadas hasta que el servidor las rechaza.
    /// Esta accion responde SIEMPRE 401 con el mismo realm; el navegador reintenta con las credenciales guardadas, vuelve a recibir
    /// 401, las descarta y muestra el cuadro de usuario y clave (basta con cancelarlo) y esta pagina.
    /// </summary>
    public IActionResult Salir()
    {
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{RealmBasicAuth}\"";
        Response.Headers.CacheControl = "no-store";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return View();
    }
}
