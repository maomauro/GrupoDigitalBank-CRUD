using System.Diagnostics;
using GrupoDigitalBank.Presentacion.Models;
using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace GrupoDigitalBank.Presentacion.Controllers;

public class UsuariosController(IUsuarioClient cliente) : Controller
{
    [HttpGet]
    public IActionResult Registrar() => View(new UsuarioViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(UsuarioViewModel modelo)
    {
        if (!ModelState.IsValid) return View(modelo);
        try
        {
            await cliente.AgregarAsync(modelo.ADto());
            TempData["Mensaje"] = "Usuario registrado correctamente.";
            return RedirectToAction(nameof(InicioController.Panel), "Inicio");
        }
        catch (ServicioException ex)
        {
            AgregarErrores(ex);
            return View(modelo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var usuario = (await cliente.ConsultarAsync(id)).Single();
            return View(UsuarioViewModel.DesdeDto(usuario));
        }
        catch (ServicioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(InicioController.Panel), "Inicio");
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(UsuarioViewModel modelo)
    {
        if (!ModelState.IsValid) return View(modelo);
        try
        {
            await cliente.ModificarAsync(modelo.ADto());
            TempData["Mensaje"] = "Usuario modificado correctamente.";
            return RedirectToAction(nameof(InicioController.Panel), "Inicio");
        }
        catch (ServicioException ex)
        {
            AgregarErrores(ex);
            return View(modelo);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, int pagina = 1, int tamano = PaginaViewModel.TamanoPorOmision)
    {
        try
        {
            await cliente.EliminarAsync(id);
            TempData["Mensaje"] = "Usuario eliminado correctamente.";
        }
        catch (ServicioException ex)
        {
            TempData["Error"] = ex.Message;
        }
        // Vuelve a la pagina desde la que se elimino; si ya no existe, el servicio devuelve la ultima
        return RedirectToAction(nameof(InicioController.Panel), "Inicio", new { pagina, tamano });
    }

    [ResponseCache(Duration = 0, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);

    private void AgregarErrores(ServicioException ex)
    {
        if (ex.Detalles.Count == 0) ModelState.AddModelError(string.Empty, ex.Message);
        foreach (var detalle in ex.Detalles) ModelState.AddModelError(string.Empty, detalle);
    }
}
