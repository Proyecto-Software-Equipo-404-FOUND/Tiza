using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using PEI.Identidad.Api.Controllers;

namespace PEI.Identidad.Api.Security;

public sealed class IdentitySecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Los errores de sesión usan ProblemDetails, incluyendo los de validación y rate limiting.
        if (context.MethodInfo.DeclaringType == typeof(AuthController))
            foreach (var status in new[] { "400", "401", "429", "500" })
                if (operation.Responses.TryGetValue(status, out var response) && response.Content.Values.FirstOrDefault()?.Schema is { } schema)
                    response.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = schema }
                    };
        var attributes = context.MethodInfo.GetCustomAttributes(true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>()).ToArray();
        operation.Security = new List<OpenApiSecurityRequirement>();
        if (attributes.OfType<IAllowAnonymous>().Any()) return;
        var authorization = attributes.OfType<IAuthorizeData>().ToArray();
        if (authorization.Length == 0) return;
        var scheme = authorization.Any(a => a.Policy == PlataformaSetup.Policy) ? PlataformaSetup.Scheme : "Bearer";
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = scheme } }] = Array.Empty<string>()
        });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Credenciales ausentes o inválidas para este esquema." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "El rol no permite esta operación." });
    }
}
