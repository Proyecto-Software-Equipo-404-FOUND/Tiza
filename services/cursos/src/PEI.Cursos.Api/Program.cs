using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.Logging;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application;
using PEI.Cursos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configuracion de controladores
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

/* Hallazgo H2: Cursos no validaba los JWT emitidos por Identidad.
Se configura la autenticacion JWT Bearer vinculando emisor, audiencia y clave compartida
para que HttpContext.User se popule correctamente antes del middleware multi-tenant. */
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PEI.Identidad";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PEI";
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("No se configuro la clave secreta Jwt:SecretKey.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Permite llamadas HTTP locales en desarrollo
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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Configuracion de Swagger enriquecida con soporte para esquema Bearer (Pilar 2)
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PEI - Microservicio Cursos",
        Version = "v1",
        Description = "API de gestion de cursos, cohortes y matriculas con aislamiento multi-tenant."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingrese el token JWT en formato: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            securityScheme,
            Array.Empty<string>()
        }
    });
});

// Registro de capas de Application e Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Contexto multi-tenant por solicitud
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp =>
    sp.GetRequiredService<TenantContext>());

var app = builder.Build();

// Middlewares transversales de BuildingBlocks en pipeline ordenado
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();

/* UseAuthentication debe preceder a TenantContextMiddleware para que
HttpContext.User ya este autenticado al extraer la institucion y el usuario. */
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Siembra de datos semilla en segundo plano para desarrollo local
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PEI.Cursos.Infrastructure.Persistence.CursosDbContext>();
            await PEI.Cursos.Infrastructure.Persistence.DataSeeder.SembrarAsync(db, app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sembrar la base de datos inicial de Cursos.");
        }
    }));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
