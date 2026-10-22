using ModelContextProtocol.AspNetCore;

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
    .WithToolsFromAssembly();

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
