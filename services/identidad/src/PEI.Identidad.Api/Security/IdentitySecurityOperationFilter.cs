using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using PEI.Identidad.Api.Controllers;
using PEI.Identidad.Api.Contracts;
using PEI.Identidad.Application.Plataforma;

namespace PEI.Identidad.Api.Security;

// Solo modifica el documento OpenAPI; no interviene en la ejecuciÃ³n HTTP.
public sealed class IdentitySecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var type = context.MethodInfo.DeclaringType;
        if (type != typeof(AuthController) && type != typeof(PlataformaController)) return;
        var platform = type == typeof(PlataformaController);
        var method = context.MethodInfo.Name;
        var attributes = context.MethodInfo.GetCustomAttributes(true)
            .Concat(type.GetCustomAttributes(true)).ToArray();
        var anonymous = attributes.OfType<IAllowAnonymous>().Any();
        var authorization = attributes.OfType<IAuthorizeData>().ToArray();
        var scheme = platform ? PlataformaSetup.Scheme : "Bearer";
        operation.Security = new List<OpenApiSecurityRequirement>();
        if (!anonymous && authorization.Length > 0)
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = scheme } }] = Array.Empty<string>()
            });

        operation.Description = platform
            ? "Requiere Platform:Enabled. Si estÃ¡ deshabilitada, las acciones devuelven 404 cuando se alcanzan; autenticaciÃ³n y autorizaciÃ³n pueden rechazar antes. JWT global separado del institucional."
            : "Identidad institucional; los JWT globales no habilitan operaciones institucionales protegidas.";
        if (!anonymous)
        {
            var role = platform ? "AdministradorGlobal" : method switch
            {
                "SoloDocente" => "Docente", "SoloAlumno" => "Alumno", _ => "Administrador"
            };
            operation.Description += $" Rol autorizado: {role}.";
        }
        if (!platform && method is "Refresh" or "Logout")
            operation.Description += " No requiere access token: recibe refreshToken en el cuerpo. Logout es idempotente; refresh rota y consume el token anterior.";
        if (!platform && method == "Register")
            operation.Description += " Solo crea Docente o Alumno en la instituciÃ³n del administrador. No admite institucionId enviado por el cliente.";
        if (platform && method == "Listar")
            operation.Description += " pagina entre 1 y 1000000; hasta 50 instituciones por pÃ¡gina, ordenadas por Id.";

        // Los IActionResult sin tipo no permiten inferir el esquema de Ã©xito.
        var (status, success) = (platform, method) switch
        {
            (false, "Login" or "Refresh") => ("200", typeof(SessionResponse)),
            (false, "Logout") => ("204", (Type?)null),
            (false, "Register") => ("201", typeof(RegistrationResponseDocumentation)),
            (false, _) => ("200", typeof(RoleResponseDocumentation)),
            (true, "Login") => ("200", typeof(PlatformLoginResponseDocumentation)),
            (true, "Crear") => ("201", typeof(InstitucionCreada)),
            (true, "Listar") => ("200", typeof(InstitucionResumen[])),
            _ => ("204", (Type?)null)
        };
        operation.Responses.Clear();
        operation.Responses[status] = success is null ? new OpenApiResponse { Description = "OperaciÃ³n completada sin cuerpo." }
            : Response("OperaciÃ³n completada.", success, "application/json");
        var post = context.ApiDescription.HttpMethod == "POST";
        if (post || method == "Listar") Error("400", "Solicitud invÃ¡lida. La validaciÃ³n automÃ¡tica puede incluir errors.");
        if (!anonymous || method is "Login" or "Refresh" or "Activar") Error("401", "Credenciales o token invÃ¡lidos.");
        if (!anonymous) Error("403", "Rol o polÃ­tica no autorizados.");
        if (platform) Error("404", "Plataforma deshabilitada, si la solicitud alcanza la acciÃ³n.");
        if (method is "Register" or "Crear") Error("409", "Conflicto de datos; no revela el propietario del correo.");
        if (post) Error("415", "Tipo de contenido no admitido.");
        if (anonymous) Error("429", "LÃ­mite de solicitudes excedido.");
        Error("500", "Error tÃ©cnico.");

        // PlatformBearer conserva el Content-Type application/json de sus challenges actuales.
        if (platform && !anonymous)
            foreach (var code in new[] { "401", "403" })
                operation.Responses[code].Content["application/json"] = new OpenApiMediaType
                { Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository) };

        OpenApiResponse Response(string description, Type body, string media) => new()
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            { [media] = new() { Schema = context.SchemaGenerator.GenerateSchema(body, context.SchemaRepository) } }
        };
        void Error(string code, string description)
        {
            operation.Responses[code] = Response(description, typeof(ProblemDetails), "application/problem+json");
            if (code == "400")
            {
                // ValidationProblemDetails aÃ±ade errors; otros 400 usan ProblemDetails simple.
                operation.Responses[code].Content["application/problem+json"].Schema = new OpenApiSchema
                {
                    AnyOf = new List<OpenApiSchema>
                    {
                        context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository),
                        context.SchemaGenerator.GenerateSchema(typeof(ValidationProblemDetails), context.SchemaRepository)
                    }
                };
            }
        }
    }
}
