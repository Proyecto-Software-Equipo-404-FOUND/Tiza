using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PEI.Evaluaciones.Api;

public sealed class ApiKeyInternaOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var ruta = context.ApiDescription.RelativePath ?? string.Empty;

        if (!ruta.StartsWith("interno/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKeyInterna"
                    }
                }
            ] = new List<string>()
        });
    }
}