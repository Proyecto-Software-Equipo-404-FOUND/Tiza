namespace PEI.Identidad.Application.Abstractions;

public sealed record RefreshIdentity(Guid UsuarioId, Guid InstitucionId, string Email, string Rol);
