using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PEI.Cursos.Api.Security;

public sealed class CursosAuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var roles = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Select(metadata => metadata.Policy switch
            {
                CursosAuthorization.SoloAdministrador => "Administrador",
                CursosAuthorization.AdministradorODocente => "Administrador o Docente",
                CursosAuthorization.GestionOAlumnoPropio => "Administrador, Docente o el propio Alumno",
                _ => null
            })
            .FirstOrDefault(value => value is not null);

        if (roles is null) return;

        operation.Responses["401"] = new OpenApiResponse { Description = "JWT ausente o inválido." };
        operation.Responses["403"] = new OpenApiResponse { Description = $"Rol no autorizado. Roles permitidos: {roles}." };
    }
}
