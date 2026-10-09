using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using GrupoDigitalBank.Negocios.Contratos;
using GrupoDigitalBank.Negocios.Datos;
using GrupoDigitalBank.Negocios.Servicios;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration.GetConnectionString("Postgres")
             ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.");

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

    // WSDL en /UsuarioService.svc?wsdl (solo red interna: este servicio no se expone al exterior).
    app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
