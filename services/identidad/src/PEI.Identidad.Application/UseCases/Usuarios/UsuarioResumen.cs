namespace PEI.Identidad.Application.UseCases.Usuarios;

// S2-ID-10: datos públicos de un usuario para elegir docentes y alumnos por nombre.
// Nunca incluye PasswordHash (contrato identidad-v1.json, UsuarioResumen).
public sealed record UsuarioResumen(
    Guid Id, string Nombre, string Apellido, string Email, string Rol, bool Activo = true, bool PendienteActivacion = false);

// S3-ID-01: los alumnos importados sin contraseña inicial quedan inactivos con esta marca en lugar de la contraseña,
// hasta que activan su cuenta con la invitación. No son "dados de baja" y no se pueden reactivar a mano.
public static class UsuarioPendiente
{
    public const string MarcaPasswordHash = "!pendiente-activacion";
}
