using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Api.Security;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Refresh;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Infrastructure.Persistence;
<<<<<<< HEAD
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
=======
using PEI.Identidad.Infrastructure.Tenancy;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Repositories;
using PEI.Identidad.Infrastructure.Security;

var generateTestToken = args.Contains("--generate-test-token", StringComparer.Ordinal);
var bootstrapPlatform = args.Contains("--bootstrap-platform", StringComparer.Ordinal);
var readDevelopmentMail = args.Contains("--read-development-mail", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a is not "--generate-test-token" and not "--bootstrap-platform" and not "--read-development-mail").ToArray());
if (generateTestToken)
>>>>>>> origin/integration/ID-04-ID-06
{
    Console.WriteLine(DevelopmentTokenCommand.Generate(builder.Configuration, builder.Environment.EnvironmentName));
    return;
}
var connectionString = builder.Configuration.GetConnectionString("IdentidadDb")
    ?? throw new InvalidOperationException("No se configuro la conexion IdentidadDb.");
builder.Services.AddDbContext<IdentidadDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<PlataformaDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistoryPlataforma")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuthenticationDataAccess, AuthenticationDataAccess>();
builder.Services.AddScoped<IAuthenticationTokenWriter, AuthenticationTokenWriter>();
builder.Services.AddScoped<IRegistrationDataAccess, RegistrationDataAccess>();
builder.Services.AddScoped<IAuthenticatedUserValidator, AuthenticatedUserValidator>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAccessTokenIssuer, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRefreshTokenHasher, RefreshTokenHasher>();
builder.Services.AddScoped<InstitutionalSessionService>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddIdentityAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddPlataforma(builder.Configuration, builder.Environment);
builder.Services.AddExceptionHandler<PEI.Identidad.Api.Errors.TenantAccessDeniedHandler>();
builder.Services.AddExceptionHandler<PEI.Identidad.Api.Errors.IdentityExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    foreach (var scheme in new[] { "Bearer", PlataformaSetup.Scheme })
        o.AddSecurityDefinition(scheme, new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = scheme == "Bearer" ? "JWT institucional con institucion_id." : "JWT global exclusivo de plataforma; sin institucion_id."
        });
    o.OperationFilter<IdentitySecurityOperationFilter>();
});
var loginLimit = builder.Configuration.GetValue<int?>("LoginRateLimit:PermitLimit") ?? 10;
if (loginLimit < 1) throw new InvalidOperationException("LoginRateLimit:PermitLimit debe ser positivo.");
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status = 429, Title = "Demasiados intentos. Intente nuevamente mas tarde." },
        options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: cancellationToken);
});
var app = builder.Build();
if (bootstrapPlatform || readDevelopmentMail)
{
    if (!builder.Configuration.GetValue<bool>("Platform:Enabled")) throw new InvalidOperationException("Habilitar Platform explícitamente.");
    await using var scope = app.Services.CreateAsyncScope();
    if (bootstrapPlatform)
    {
        if (!builder.Configuration.GetValue<bool>("PlatformBootstrap:Enabled")) throw new InvalidOperationException("Bootstrap no habilitado explícitamente.");
        var config = builder.Configuration.GetSection("PlatformBootstrap");
        await scope.ServiceProvider.GetRequiredService<PEI.Identidad.Infrastructure.Plataforma.PlataformaBootstrap>().EjecutarAsync(
            config["Email"] ?? "", config["Password"] ?? "", config["Nombre"] ?? "", config["Apellido"] ?? "", config["Operador"] ?? "", CancellationToken.None);
        Console.WriteLine("Bootstrap completado. Retirar secretos y deshabilitar PlatformBootstrap.");
    }
    else
    {
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Buzón disponible solo en Development.");
        var id = Guid.Parse(builder.Configuration["PlatformDevelopment:MailId"] ?? "");
        var options = scope.ServiceProvider.GetRequiredService<PEI.Identidad.Infrastructure.Security.PlataformaOptions>();
        var path = Path.Combine(options.DevelopmentMailDirectory, id.ToString("N") + ".mail");
        var protection = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        Console.WriteLine(Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Unprotect(
            protection.CreateProtector(PEI.Identidad.Infrastructure.Plataforma.DevelopmentEmailSender.Purpose), await File.ReadAllTextAsync(path)));
    }
    return; // Nunca publicar HTTP durante operaciones de consola.
}
if (builder.Configuration.GetValue<bool>("DevelopmentTools:EnableTestTokens"))
    DevelopmentTokenCommand.ValidateConfiguredBindings(builder.Configuration);
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseMiddleware<PEI.BuildingBlocks.Logging.CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    var correlationId = context.Items[PEI.BuildingBlocks.Logging.CorrelationIdMiddleware.CorrelationIdHeader]?.ToString()
        ?? context.TraceIdentifier;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[PEI.BuildingBlocks.Logging.CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;
        return Task.CompletedTask;
    });
    using (app.Logger.BeginScope(new Dictionary<string, object?>
    {
        ["CorrelationId"] = context.Items[PEI.BuildingBlocks.Logging.CorrelationIdMiddleware.CorrelationIdHeader]
    }))
    {
        app.Logger.LogInformation("Solicitud HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
        await next();
    }
});
app.UseExceptionHandler();
app.UseHttpsRedirection();
<<<<<<< HEAD

app.UseExceptionHandler();

app.UseAuthentication();

=======
app.UseAuthentication();
>>>>>>> origin/integration/ID-04-ID-06
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program { }
