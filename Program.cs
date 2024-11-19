using DiscordBotAPI.Services;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Registra en la consola
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // Registra en archivos diarios
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog(); // Reemplaza el sistema de logging predeterminado

// Agregar servicios al contenedor
builder.Services.AddControllers();
builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddSingleton<OpenAIService>();

// Registrar Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DiscordBotAPI", Version = "v1" });
});

var app = builder.Build();

// Configuración del pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DiscordBotAPI v1");
        c.RoutePrefix = string.Empty; // Establece Swagger como la raíz
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Iniciando la aplicación");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
}
finally
{
    Log.CloseAndFlush(); // Asegura que se liberen los recursos de Serilog
}
