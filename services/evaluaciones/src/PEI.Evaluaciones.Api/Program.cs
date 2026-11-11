using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PEI.Evaluaciones.Api;
using PEI.Evaluaciones.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<EvaluacionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PEI.Evaluaciones.Application.Evaluaciones.CrearEvaluacionCommand).Assembly));
    
builder.Services.AddSingleton<PEI.Evaluaciones.Application.Validacion.ValidadorPreguntas>();

builder.Services.AddScoped<
    PEI.Evaluaciones.Application.Evaluaciones.IResultadosRepositorio,
    PEI.Evaluaciones.Infrastructure.Persistence.ResultadosRepositorio>();

builder.Services.AddScoped<PEI.Evaluaciones.Application.Evaluaciones.ICursosClient, PEI.Evaluaciones.Infrastructure.Clientes.CursosClientProvisorio>();

// S2-EV-02 (#144): corrección con IA
builder.Services.AddScoped<
    PEI.Evaluaciones.Application.Correccion.ICorreccionRepositorio,
    PEI.Evaluaciones.Infrastructure.Persistence.CorreccionRepositorio>();
builder.Services.AddHttpClient<
    PEI.Evaluaciones.Application.Correccion.ICorreccionIaClient,
    PEI.Evaluaciones.Infrastructure.Clientes.CorreccionIaHttpClient>(cliente =>
{
    cliente.BaseAddress = new Uri(builder.Configuration["CorreccionIa:UrlBaseIa"] ?? "http://localhost:5105/");
    cliente.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHostedService<PEI.Evaluaciones.Api.Workers.CorreccionIaWorker>();    

// Configuración de Autenticación JWT Institucional (identidad-v1.json / S2-EV-03)
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PEI.Identidad";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PEI";
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? "DesarrolloLocalSecretoCompartidoPEI2026_ClaveSegura32BytesMinimo!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // Contrato institucional
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DocenteOAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Docente", "Administrador"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Esquema Bearer JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT institucional de Identidad. Ingrese: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Esquema ApiKey interna
    options.AddSecurityDefinition("ApiKeyInterna", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key-Interna",
        Description = "Clave interna entre servicios."
    });

    options.OperationFilter<ApiKeyInternaOperationFilter>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PEI.Evaluaciones.Infrastructure.Persistence.EvaluacionDbContext>();
            var validador = scope.ServiceProvider.GetRequiredService<PEI.Evaluaciones.Application.Validacion.ValidadorPreguntas>();
            await PEI.Evaluaciones.Infrastructure.Persistence.EvaluacionSeedData.SembrarAsync(db, validador, app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sembrar la base de datos de evaluaciones.");
        }
    }));
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// UseAuthentication DEBE ir antes de UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();