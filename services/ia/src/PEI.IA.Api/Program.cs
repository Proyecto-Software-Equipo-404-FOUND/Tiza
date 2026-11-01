using Microsoft.OpenApi.Models;
using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.Logging;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Prompts;
using PEI.IA.Application.Services;
using PEI.IA.Infrastructure.Evaluaciones;
using PEI.IA.Infrastructure.LlmClients;
using PEI.IA.Infrastructure.Mcp;
using PEI.IA.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración ──
var mcpSettings = builder.Configuration.GetSection("McpSettings").Get<McpSettings>() ?? new McpSettings();
builder.Services.AddSingleton(mcpSettings);

var llmSettings = builder.Configuration.GetSection("LlmSettings").Get<LlmSettings>() ?? new LlmSettings();
builder.Services.AddSingleton(llmSettings);

var resilienceOptions = builder.Configuration.GetSection("LlmResilienceOptions").Get<LlmResilienceOptions>() ?? new LlmResilienceOptions();
builder.Services.AddSingleton(resilienceOptions);

// ── Repositorio de Auditoría de Solicitudes IA (SolicitudCorreccion) ──
builder.Services.AddSingleton<ISolicitudCorreccionRepository, InMemorySolicitudCorreccionRepository>();

// ── Cliente LLM Resiliente con Polly (Conmutación automática + Circuit Breaker) ──
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ILlmClient, PollyResilientLlmClient>();

// ── Servicios de aplicación ──
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<ICorreccionService, CorreccionService>();

// ── Tool Executor (Plan B o MCP real) ──
builder.Services.AddSingleton<LocalToolExecutor>();
builder.Services.AddHttpClient<McpToolExecutor>();
builder.Services.AddTransient<IToolExecutor>(sp =>
{
    var settings = sp.GetRequiredService<McpSettings>();
    return settings.UsarPlanB
        ? sp.GetRequiredService<LocalToolExecutor>()
        : sp.GetRequiredService<McpToolExecutor>();
});

// ── Cliente HTTP → Microservicio de Evaluaciones (EV-06) ──
var evaluacionesSettings = builder.Configuration
    .GetSection("EvaluacionesSettings")
    .Get<EvaluacionesSettings>() ?? new EvaluacionesSettings();
builder.Services.AddSingleton(evaluacionesSettings);
builder.Services.AddHttpClient<IEvaluacionesClient, EvaluacionesHttpClient>(client =>
{
    client.BaseAddress = new Uri(evaluacionesSettings.BaseUrl.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromMilliseconds(evaluacionesSettings.TimeoutMs);
});

// ── Controllers + JSON ──
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ── Swagger / OpenAPI ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PEI Tiza — Servicio de Inteligencia Artificial",
        Version = "v1",
        Description = "Endpoints internos de corrección automatizada de respuestas abiertas usando IA.",
        Contact = new OpenApiContact
        {
            Name = "Equipo 404 Found",
        }
    });

    // Incluir XML comments si existen
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// ── Middlewares de BuildingBlocks ──
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();

// ── Swagger (solo en Development) ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PEI.IA v1");
        options.DocumentTitle = "PEI Tiza — IA API";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
