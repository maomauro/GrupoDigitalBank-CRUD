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
            return RedirectToAction(nameof(Consultar));
        }
        catch (ServicioException ex)
        {
            AgregarErrores(ex);
            return View(modelo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        try
        {
            var usuarios = await cliente.ConsultarAsync();
            return View(usuarios.Select(UsuarioViewModel.DesdeDto).ToList());
        }
        catch (ServicioException ex)
        {
            AgregarErrores(ex);
            return View(new List<UsuarioViewModel>());
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
            return RedirectToAction(nameof(Consultar));
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
            return RedirectToAction(nameof(Consultar));
        }
        catch (ServicioException ex)
        {
            AgregarErrores(ex);
            return View(modelo);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
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
        return RedirectToAction(nameof(Consultar));
    }

    [ResponseCache(Duration = 0, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);

    private void AgregarErrores(ServicioException ex)
    {
        if (ex.Detalles.Count == 0) ModelState.AddModelError(string.Empty, ex.Message);
        foreach (var detalle in ex.Detalles) ModelState.AddModelError(string.Empty, detalle);
    }
}
