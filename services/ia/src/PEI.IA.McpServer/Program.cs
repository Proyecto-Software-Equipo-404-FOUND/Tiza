using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol.Types;
using PEI.IA.McpServer.Resources;

var builder = WebApplication.CreateBuilder(args);

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
