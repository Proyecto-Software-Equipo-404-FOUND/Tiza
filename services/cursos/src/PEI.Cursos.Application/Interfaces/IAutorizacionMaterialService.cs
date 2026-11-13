using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato para la autorizacion y validacion de reglas de acceso sobre materiales de curso (SRP / DIP).
Garantiza el cumplimiento estricto de las reglas RN-MAT-01 y RN-MAT-02 y la regla anti-enumeracion. */
public interface IAutorizacionMaterialService
{
    // Valida que el curso exista (404) y que el usuario sea Docente asignado a cohorte activa o Admin (403).
    Task ValidarPermisoGestionAsync(Guid cursoId, CancellationToken cancellationToken = default);

    // Valida que el curso exista en la institucion (404) admitiendo cursos inactivos en modo historico, y que el usuario sea Docente con cohorte o Admin (403).
    Task ValidarPermisoLecturaAsync(Guid cursoId, CancellationToken cancellationToken = default);

    // Valida que el usuario no sea Alumno (403) para el acceso al texto interno del apunte.
    void ValidarAccesoTextoInterno();
}
