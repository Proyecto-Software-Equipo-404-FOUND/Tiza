using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Infrastructure.Persistence;
using PEI.SalaEnVivo.Infrastructure.Repositories;
using PEI.SalaEnVivo.Infrastructure.Services;
using PEI.SalaEnVivo.Infrastructure.Clientes;
using PEI.SalaEnVivo.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<pei_sala_db>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISalaRepository, SalaRepository>();
builder.Services.AddScoped<IParticipanteSalaRepository, ParticipanteSalaRepository>();
builder.Services.AddScoped<IRespuestaSalaRepository, RespuestaSalaRepository>();

var redisConn = builder.Configuration.GetConnectionString("Redis")
                ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        try
        {
            var config = ConfigurationOptions.Parse(redisConn);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        }
        catch (Exception ex)
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "No se pudo conectar a Redis al inicio ({RedisConn}). Se continuará con fallback a base de datos.", redisConn);
            return null!;
        }
    });
}

builder.Services.AddScoped<ISalaCronometroService, SalaCronometroService>();

var evaluacionesBaseUrl = builder.Configuration["Evaluaciones:BaseUrl"]
                          ?? "http://localhost:5103";

builder.Services.AddHttpClient<IEvaluacionesClient, EvaluacionesClient>(client =>
{
    client.BaseAddress = new Uri(evaluacionesBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<CrearSalaCommandHandler>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(UnirseASalaPorPINCommandHandler).Assembly));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience) ||
    string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT configuration is missing. Configure Jwt:Issuer, " +
        "Jwt:Audience, and Jwt:SecretKey.");
}


builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),

            ValidateLifetime = true
        };
        
        // Read access_token from the query string for SignalR.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var requestPath = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    requestPath.StartsWithSegments("/hubs/sala"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<pei_sala_db>();

    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al crear la base de datos.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SalaHub>("/hubs/sala");

app.Run();
