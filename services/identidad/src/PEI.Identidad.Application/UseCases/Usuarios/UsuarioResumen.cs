namespace PEI.Identidad.Application.UseCases.Usuarios;

// S2-ID-10: datos públicos de un usuario para elegir docentes y alumnos por nombre.
// Nunca incluye PasswordHash (contrato identidad-v1.json, UsuarioResumen).
public sealed record UsuarioResumen(Guid Id, string Nombre, string Apellido, string Email, string Rol);
