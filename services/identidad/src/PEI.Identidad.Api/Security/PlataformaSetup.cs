using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Infrastructure.Plataforma;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.Api.Security;

public static class PlataformaSetup
{
    public const string Scheme = "PlatformBearer";
    public const string Policy = "PlatformAdmin";
    public static void AddPlataforma(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection("Platform").Get<PlataformaOptions>() ?? new();
        services.AddSingleton(options);

        // El envío de invitaciones es una capacidad institucional y de plataforma;
        // no depende de que las rutas globales estén habilitadas.
        var dataProtection = services.AddDataProtection().SetApplicationName("Tiza.Identidad.Plataforma");
        if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
        if (!string.IsNullOrWhiteSpace(options.KeyDirectory))
        {
            if (!Path.IsPathFullyQualified(options.KeyDirectory))
                throw new InvalidOperationException("Configurar un directorio absoluto para las claves de Data Protection.");
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.KeyDirectory));
        }
        services.AddScoped<InvitacionesOutbox>();
        if (environment.IsDevelopment()) services.AddScoped<IEmailSender, DevelopmentEmailSender>();
        services.AddHostedService<InvitationSenderRegistrationValidator>();
        services.AddHostedService<InvitacionesWorker>();

        if (!options.Enabled)
        {
            services.AddAuthorization(o => o.AddPolicy(Policy, p => p.RequireAssertion(_ => false)));
            return;
        }
        var institucional = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
        options.Validate(institucional, environment.IsDevelopment());
        if (configuration.GetValue<bool>("DevelopmentTools:EnableTestTokens"))
        {
            var local = DevelopmentTokenCommand.ReadOptions(configuration);
            if (local.SecretKey == options.Jwt.SecretKey || local.Issuer == options.Jwt.Issuer || local.Audience == options.Jwt.Audience)
                throw new InvalidOperationException("La confianza local y la de plataforma deben estar separadas.");
        }
        services.AddScoped<IPlataformaDataAccess, PlataformaDataAccess>();
        services.AddScoped<IPlataformaTokenService, PlataformaTokenService>();
        services.AddScoped<PlataformaHandlers>();
        services.AddScoped<PlataformaBootstrap>();
        services.AddAuthentication().AddJwtBearer(Scheme, o =>
        {
            o.MapInboundClaims = false; o.IncludeErrorDetails = false;
            o.TokenValidationParameters = options.Jwt.ValidationParameters();
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async c =>
                {
                    if (c.Principal is null || !JwtContract.HasUniquePayloadKeys(c.SecurityToken) || !PlataformaJwtContract.TryRead(c.Principal, out var id) ||
                        !await c.HttpContext.RequestServices.GetRequiredService<IPlataformaDataAccess>().EstaActivaAsync(id, c.HttpContext.RequestAborted))
                        c.Fail("Credenciales inválidas.");
                },
                OnChallenge = async c =>
                {
                    c.HandleResponse(); if (c.Response.HasStarted) return;
                    c.Response.StatusCode = 401; c.Response.Headers.WWWAuthenticate = "Bearer";
                    await c.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 401, Title = "Credenciales inválidas." });
                },
                OnForbidden = async c =>
                {
                    if (c.Response.HasStarted) return; c.Response.StatusCode = 403;
                    await c.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 403, Title = "Operación no autorizada." });
                }
            };
        });
        services.AddAuthorization(o => o.AddPolicy(Policy, new AuthorizationPolicyBuilder(Scheme)
            .RequireAuthenticatedUser().RequireRole("AdministradorGlobal").RequireClaim("identity_type", "platform").Build()));
    }
}
