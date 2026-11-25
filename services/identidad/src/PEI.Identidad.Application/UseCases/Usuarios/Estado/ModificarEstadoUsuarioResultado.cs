namespace PEI.Identidad.Application.UseCases.Usuarios.Estado;

public enum ModificarEstadoUsuarioResultado
{
    Exitoso,
    NoEncontrado,
    RolNoPermitido,
    // S3-ID-01: el alumno nunca activó su cuenta (importado sin contraseña inicial)
    PendienteActivacion
}
