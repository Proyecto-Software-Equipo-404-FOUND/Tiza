using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Infrastructure.Persistence;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Application.UseCases.Auth.Refresh;
using PEI.Identidad.Infrastructure.Repositories;
using PEI.Identidad.Infrastructure.Security;
using System.Security.Claims;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

// Conexion a PostgreSQL
var connectionString = builder.Configuration
    .GetConnectionString("IdentidadDb")
    ?? throw new InvalidOperationException(
        "No se configuro la conexion IdentidadDb.");

builder.Services.AddDbContext<IdentidadDbContext>(options =>
    options.UseNpgsql(connectionString));



// Servicios de la API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresá el access token JWT."
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
});

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRefreshTokenHasher, RefreshTokenHasher>();

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Issuer.");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Audience.");

var secretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:SecretKey.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();




// Construccion de la aplicacion
var app = builder.Build();

// Trazabilidad de solicitudes
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"]
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
    }

    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using (app.Logger.BeginScope(
        new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
    {
        app.Logger.LogInformation(
            "Solicitud HTTP {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        await next();
    }
});



// Configuracion del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
