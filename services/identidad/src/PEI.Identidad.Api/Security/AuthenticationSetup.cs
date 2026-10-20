using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Security;
namespace PEI.Identidad.Api.Security;
public static class AuthenticationSetup
{
    public static void AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
        if (!jwt.IsValid()) throw new InvalidOperationException("Configurar Jwt:SecretKey, Issuer, Audience y duracion validos.");
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt")).Validate(o => o.IsValid(), "Configuracion JWT invalida.").ValidateOnStart();
        var auth = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        auth.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o => Configure(o, jwt));
        var schemes = new List<string> { JwtBearerDefaults.AuthenticationScheme };
        if (configuration.GetValue<bool>("DevelopmentTools:EnableTestTokens"))
        {
            DevelopmentTokenCommand.ValidateEnvironment(configuration, environment.EnvironmentName);
            var development = DevelopmentTokenCommand.ReadOptions(configuration);
            auth.AddJwtBearer("LocalDevelopment", o => Configure(o, development));
            schemes.Add("LocalDevelopment");
        }
        services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicyBuilder(schemes.ToArray()).RequireAuthenticatedUser().Build();
            o.AddPolicy("InstitutionAdmin", new AuthorizationPolicyBuilder(schemes.ToArray())
                .RequireAuthenticatedUser().RequireRole(IdentityRoles.Administrador).Build());
        });
    }
    private static void Configure(JwtBearerOptions options, JwtOptions jwt)
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = jwt.ValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 401, Title = "Credenciales invalidas." },
                    options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 403, Title = "Operacion no autorizada." },
                    options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
            },
            OnTokenValidated = async context =>
            {
                if (context.Principal is null || !JwtContract.HasUniquePayloadKeys(context.SecurityToken) || !JwtContract.TryRead(context.Principal, out var usuario, out var institucion, out var role))
                { context.Fail("Credenciales invalidas."); return; }
                var validator = context.HttpContext.RequestServices.GetRequiredService<IAuthenticatedUserValidator>();
                if (!await validator.IsActiveAsync(usuario, institucion, role, context.HttpContext.RequestAborted))
                    context.Fail("Credenciales invalidas.");
            }
        };
    }
}
