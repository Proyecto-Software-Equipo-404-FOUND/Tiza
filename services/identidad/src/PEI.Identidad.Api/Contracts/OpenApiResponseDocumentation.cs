using System.ComponentModel.DataAnnotations;

namespace PEI.Identidad.Api.Contracts;

// Tipos exclusivos de metadatos Swagger. Las acciones conservan sus respuestas actuales.
public sealed record RegistrationResponseDocumentation([property: Required] Guid Id, [property: Required] string Message);
public sealed record RoleResponseDocumentation([property: Required] string Mensaje);
public sealed record PlatformLoginResponseDocumentation([property: Required] string AccessToken, [property: Required] string TokenType);
