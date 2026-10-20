using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Api.Security;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Tenancy;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Repositories;
using PEI.Identidad.Infrastructure.Security;

var generateTestToken = args.Contains("--generate-test-token", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--generate-test-token").ToArray());
if (generateTestToken)
{
    Console.WriteLine(DevelopmentTokenCommand.Generate(builder.Configuration, builder.Environment.EnvironmentName));
    return;
}
var connectionString = builder.Configuration.GetConnectionString("IdentidadDb")
    ?? throw new InvalidOperationException("No se configuro la conexion IdentidadDb.");
builder.Services.AddDbContext<IdentidadDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuthenticationDataAccess, AuthenticationDataAccess>();
builder.Services.AddScoped<IAuthenticationTokenWriter, AuthenticationTokenWriter>();
builder.Services.AddScoped<IRegistrationDataAccess, RegistrationDataAccess>();
builder.Services.AddScoped<IAuthenticatedUserValidator, AuthenticatedUserValidator>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddIdentityAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddExceptionHandler<PEI.Identidad.Api.Errors.TenantAccessDeniedHandler>();
builder.Services.AddExceptionHandler<PEI.Identidad.Api.Errors.IdentityExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program { }
