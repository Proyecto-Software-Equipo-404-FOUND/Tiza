using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol.Types;
using PEI.IA.Application.Interfaces;
using PEI.IA.Infrastructure.Evaluaciones;
using PEI.IA.McpServer.Resources;

var builder = WebApplication.CreateBuilder(args);

// ── EvaluacionesHttpClient — callback hacia Evaluaciones desde la tool MCP ──
var evaluacionesSettings = builder.Configuration
    .GetSection("EvaluacionesSettings")
    .Get<EvaluacionesSettings>() ?? new EvaluacionesSettings();

// La clave interna viene de variable de entorno; nunca se hardcodea en el repo
var apiKeyFromEnv = Environment.GetEnvironmentVariable("EVALUACIONES_API_KEY_INTERNA");
if (!string.IsNullOrWhiteSpace(apiKeyFromEnv))
    evaluacionesSettings.ApiKeyInterna = apiKeyFromEnv;

builder.Services.AddSingleton(evaluacionesSettings);
builder.Services.AddHttpClient<IEvaluacionesClient, EvaluacionesHttpClient>(client =>
{
    client.BaseAddress = new Uri(evaluacionesSettings.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMilliseconds(evaluacionesSettings.TimeoutMs);
});

// ── Configurar servidor MCP con transporte HTTP stateless ──
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "PEI.IA.McpServer",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithListResourceTemplatesHandler((context, ct) =>
    {
        var templates = new List<ResourceTemplate>
        {
            new()
            {
                UriTemplate = RubricaResource.UriTemplate,
                Name = "Rúbrica de evaluación",
                Description = "Devuelve la rúbrica en formato JSON para la evaluación indicada.",
                MimeType = "application/json"
            }
        };
        return ValueTask.FromResult(new ListResourceTemplatesResult { ResourceTemplates = templates });
    })
    .WithReadResourceHandler((context, ct) =>
    {
        var uri = context.Params?.Uri ?? string.Empty;
        var evaluacionId = uri.StartsWith("rubrica://", StringComparison.OrdinalIgnoreCase)
            ? uri["rubrica://".Length..].TrimEnd('/')
            : uri;

        var json = RubricaResource.ObtenerRubrica(evaluacionId);

        var contents = new List<ResourceContents>
        {
            new TextResourceContents
            {
                Uri = uri,
                MimeType = "application/json",
                Text = json
            }
        };
        return ValueTask.FromResult(new ReadResourceResult { Contents = contents });
    });

var app = builder.Build();

// ── Mapear endpoint MCP ──
app.MapMcp();

// ── Health check simple ──
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "PEI.IA.McpServer",
    protocol = "MCP",
    timestamp = DateTime.UtcNow
}));

app.Run();

