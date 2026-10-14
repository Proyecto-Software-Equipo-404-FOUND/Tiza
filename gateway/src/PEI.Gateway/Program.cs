using PEI.BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configurar Reverse Proxy YARP desde appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseCors("CorsPolicy");

// Propagación de Correlation ID
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    Status = "Online",
    Service = "PEI API Gateway (YARP)",
    Timestamp = DateTime.UtcNow
}));

app.MapReverseProxy();

app.Run();
