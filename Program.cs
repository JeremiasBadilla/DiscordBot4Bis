using System.Security.Cryptography.X509Certificates;
using DiscordBotAPI.Services;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configurar HTTPS con certificado de desarrollo
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5045, listenOptions =>
    {
        listenOptions.UseHttps(); // Usar HTTPS con el certificado local
    });
});

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Registrar en consola
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // Registrar en archivos diarios
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog(); // Reemplaza el sistema de logging predeterminado

// Agregar servicios al contenedor
builder.Services.AddControllers();
builder.Services.AddScoped<DiscordBotService>();
builder.Services.AddScoped<OpenAIService>();

// Registrar Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DiscordBotAPI",
        Version = "v1",
        Description = "API para interactuar con Discord y OpenAI",
        Contact = new OpenApiContact
        {
            Name = "Jeremías",
            Email = "jeremias.badilla@4bis.cl"
        }
    });
    
    // Incluir comentarios XML para la documentación (si está configurado)
    //var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    //var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    
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
 app.Use(async (context, next) =>
{
    Log.Information("Processing request: {Path}", context.Request.Path);
    await next();
});
try
{
    Log.Information("Iniciando la aplicación");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw; // Re-lanzar para capturar en otras capas si es necesario
}
finally
{
    Log.CloseAndFlush(); // Asegura liberar recursos de Serilog
}