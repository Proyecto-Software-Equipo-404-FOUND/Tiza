using System;
using PEI.Cursos.Domain.Common;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Domain.Entities;

/* Entidad de Dominio rica que representa la inscripción formal de un alumno en una cohorte.
Modela el ciclo de vida de la cursada (Activa, Baja, Completada) y protege las transiciones
de estado mediante métodos de negocio explícitos. */
public class Matriculacion : BaseEntity
{
    // Clave foránea física hacia la cohorte correspondiente en pei_cursos_db.
    public Guid CohorteId { get; private set; }

    // Referencia lógica al usuario alumno en Identidad_DB (sin FK física por Database-per-Service).
    public Guid AlumnoId { get; private set; }

    // Fecha y hora UTC en la que se efectuó el alta de la matriculación.
    public DateTime FechaAlta { get; private set; }

    // Estado actual del alumno en la cursada, tipado mediante enum de dominio.
    public EstadoMatriculacion Estado { get; private set; }

    // Propiedad de navegación relacional hacia la cohorte en Entity Framework Core.
    public virtual Cohorte Cohorte { get; private set; } = null!;

    /* Constructor de negocio. Exige las referencias obligatorias e inicializa
    la matrícula en estado Activa con fecha de alta UTC. */
    public Matriculacion(Guid institucionId, Guid cohorteId, Guid alumnoId)
    {
        if (institucionId == Guid.Empty)
            throw new DomainException("La institución es obligatoria para la matriculación.");

        if (cohorteId == Guid.Empty)
            throw new DomainException("La cohorte es obligatoria para la matriculación.");

        if (alumnoId == Guid.Empty)
            throw new DomainException("El alumno es obligatorio para la matriculación.");

        InstitucionId = institucionId;
        CohorteId = cohorteId;
        AlumnoId = alumnoId;
        Estado = EstadoMatriculacion.Activa;
        FechaAlta = DateTime.UtcNow;
    }

    // Constructor protegido sin parámetros requerido por el materializador de EF Core.
    private Matriculacion()
    {
    }

    // Da de baja la matriculación de un alumno (desuscripción o abandono de cursada).
    public void DarDeBaja()
    {
        if (Estado == EstadoMatriculacion.Baja)
            throw new DomainException("La matriculación ya se encuentra dada de baja.");

        if (Estado == EstadoMatriculacion.Completada)
            throw new DomainException("No es posible dar de baja una cursada que ya ha sido completada.");

        Estado = EstadoMatriculacion.Baja;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Marca la cursada como finalizada y aprobada/completada para el alumno.
    public void Completar()
    {
        if (Estado != EstadoMatriculacion.Activa)
            throw new DomainException("Solo se pueden marcar como completadas las matriculaciones en estado activo.");

        Estado = EstadoMatriculacion.Completada;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Permite reincorporar a un alumno previamente dado de baja a la cohorte.
    public void Reactivar()
    {
        if (Estado == EstadoMatriculacion.Activa)
            throw new DomainException("La matriculación ya se encuentra activa.");

        if (Estado == EstadoMatriculacion.Completada)
            throw new DomainException("No es posible reactivar una matriculación completada.");

        Estado = EstadoMatriculacion.Activa;
        FechaModificacionUtc = DateTime.UtcNow;
    }
}
