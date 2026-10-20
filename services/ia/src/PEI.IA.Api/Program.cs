using Microsoft.OpenApi.Models;
using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.Logging;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Services;
using PEI.IA.Infrastructure.LlmClients;
using PEI.IA.Infrastructure.Mcp;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración ──
var mcpSettings = builder.Configuration.GetSection("McpSettings").Get<McpSettings>() ?? new McpSettings();
builder.Services.AddSingleton(mcpSettings);

var llmSettings = builder.Configuration.GetSection("LlmSettings").Get<LlmSettings>() ?? new LlmSettings();
builder.Services.AddSingleton(llmSettings);

// ── Cliente LLM (OpenAI compatible: Groq, OpenRouter, Local) ──
builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleClient>();

// ── Servicios de aplicación ──
builder.Services.AddScoped<ICorreccionService, CorreccionService>();

// ── Tool Executor (Plan B o MCP real) ──
if (mcpSettings.UsarPlanB)
{
    builder.Services.AddSingleton<IToolExecutor, LocalToolExecutor>();
}
else
{
    builder.Services.AddSingleton<IToolExecutor, McpToolExecutor>();
}

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
