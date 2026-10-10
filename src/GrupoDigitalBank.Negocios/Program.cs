using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using GrupoDigitalBank.Negocios.Servicios;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Sin valor por omision: la cadena de conexion (con su contrasena) llega por entorno, user-secrets o
// appsettings.{Entorno}.json local, nunca desde un archivo versionado (hallazgo B5).
var cadena = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(cadena))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings:Postgres. Definala por variable de entorno (ConnectionStrings__Postgres), " +
        "dotnet user-secrets o un appsettings.{Entorno}.json local; no se versiona.");
}

builder.Services.AddSingleton(NpgsqlDataSource.Create(cadena));
builder.Services.AddSingleton<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddTransient<UsuarioService>();
builder.Services.AddServiceModelServices().AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<UsuarioService>(o => o.DebugBehavior.IncludeExceptionDetailInFaults = false);
    serviceBuilder.AddServiceEndpoint<UsuarioService, IUsuarioService>(new BasicHttpBinding(), "/UsuarioService.svc");

    // WSDL en /UsuarioService.svc?wsdl solo en desarrollo: expone el contrato completo del servicio y el
    // cliente de Presentacion no lo necesita (usa un contrato propio). Hallazgo B8.
    app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = app.Environment.IsDevelopment();
});

app.MapGet("/health", () => Results.Text("ok"));

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas del host de negocios.
public partial class Program;
