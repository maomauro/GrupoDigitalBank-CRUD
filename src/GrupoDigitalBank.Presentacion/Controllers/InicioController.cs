using Microsoft.AspNetCore.Mvc;

namespace GrupoDigitalBank.Presentacion.Controllers;

/// <summary>
/// Pagina de inicio publica (explica como se desarrollo la solucion) y cierre de sesion.
/// Ninguna accion consulta el servicio de negocios ni muestra datos: por eso el proxy las deja fuera del Basic Auth (ADR 0016).
/// </summary>
public sealed class InicioController : Controller
{
    /// <summary>Realm que anuncia el Basic Auth de Caddy (su valor por omision). Tiene que coincidir para que el navegador olvide las credenciales.</summary>
    internal const string RealmBasicAuth = "restricted";

    public IActionResult Index() => View();

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
