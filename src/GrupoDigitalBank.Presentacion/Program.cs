using GrupoDigitalBank.Presentacion.Servicios;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IUsuarioClient, UsuarioClient>();

// Data Protection firma el token antiforgery y las cookies (TempData). Sin persistir las claves, cada
// contenedor nuevo genera otras y un formulario abierto antes de un redespliegue falla con 400 (hallazgo H6).
// En Docker la ruta es un volumen (DataProtection__RutaClaves=/keys); en desarrollo local se omite y rige el
// valor por omision de ASP.NET Core (el perfil del usuario). El nombre de aplicacion es fijo para que las
// claves no dependan de la ruta del contenido.
var proteccion = builder.Services.AddDataProtection().SetApplicationName("GrupoDigitalBank");
var rutaClaves = builder.Configuration["DataProtection:RutaClaves"];
if (!string.IsNullOrWhiteSpace(rutaClaves))
{
    proteccion.PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
}

// Detras del proxy inverso (Caddy) dentro de la red interna de Docker.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Cookies "Secure" cuando la peticion llego por HTTPS (ADR 0011). El visitante usa HTTPS contra Cloudflare y el nginx
// compartido, pero hasta aqui llega HTTP: el esquema original viaja en X-Forwarded-Proto (ver arriba) y SameAsRequest
// lo respeta. En desarrollo local (http://localhost:8080) las cookies siguen sin Secure: una cookie Secure se
// descartaria y los formularios dejarian de funcionar. Por omision ni la cookie antiforgery ni la de TempData la llevan.
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
builder.Services.Configure<CookieTempDataProviderOptions>(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Usuarios/Error");
}

app.UseStaticFiles();
app.UseRouting();
// Liveness del proceso web: no consulta el servicio de negocios. El proxy lo deja fuera del Basic Auth.
// GET y HEAD: los monitores externos (UptimeRobot) comprueban con HEAD y recibian 405
app.MapMethods("/health", ["GET", "HEAD"], () => Results.Text("ok"));
// Inicio publico en "/" y cierre de sesion en "/salir" (ADR 0016); el resto de la aplicacion sigue detras del Basic Auth del proxy
app.MapControllerRoute("salir", "salir", new { controller = "Inicio", action = "Salir" });
// Inicio CON sesion (grilla): el proxy lo protege; "/" es el inicio publico
app.MapControllerRoute("panel", "inicio", new { controller = "Inicio", action = "Panel" });
app.MapControllerRoute("acerca", "acerca", new { controller = "Inicio", action = "Acerca" });
app.MapControllerRoute("default", "{controller=Inicio}/{action=Index}/{id?}");

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integracion web.
public partial class Program;
