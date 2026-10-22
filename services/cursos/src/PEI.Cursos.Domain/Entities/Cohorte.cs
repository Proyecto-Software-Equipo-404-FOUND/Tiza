using System;
using System.Collections.Generic;
using PEI.Cursos.Domain.Common;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Domain.Entities;

/* Entidad de Dominio rica que representa una cohorte, comisión o división específica de un curso.
Asocia un curso académico con un docente responsable durante un período lectivo determinado.
Encapsula su estado (private set) e impide instanciaciones o mutaciones inconsistentes. */
public class Cohorte : BaseEntity
{
    // Clave foránea física hacia la entidad Curso en esta misma base de datos (pei_cursos_db).
    public Guid CursoId { get; private set; }

    /* Referencia lógica al usuario docente responsable (definido en Identidad_DB).
    No posee clave foránea física en PostgreSQL para respetar la autonomía de microservicios (Database-per-Service). */
    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = null!;
    public string Periodo { get; private set; } = null!;
    public bool Activo { get; private set; }

    // Propiedades de navegación relacional para Entity Framework Core.
    public virtual Curso Curso { get; private set; } = null!;
    public virtual ICollection<Matriculacion> Matriculaciones { get; private set; } = new List<Matriculacion>();

    /* Constructor de negocio. Exige todos los identificadores y atributos requeridos,
    garantizando que la cohorte nazca en un estado válido y ligada a su institución. */
    public Cohorte(Guid institucionId, Guid cursoId, Guid docenteId, string nombre, string periodo)
    {
        if (institucionId == Guid.Empty)
            throw new DomainException("La institución es obligatoria para registrar una cohorte.");

        if (cursoId == Guid.Empty)
            throw new DomainException("El curso asociado es obligatorio para la cohorte.");

        if (docenteId == Guid.Empty)
            throw new DomainException("El docente responsable es obligatorio para la cohorte.");

        ValidarDatos(nombre, periodo);

        InstitucionId = institucionId;
        CursoId = cursoId;
        DocenteId = docenteId;
        Nombre = nombre.Trim();
        Periodo = periodo.Trim();
        Activo = true;
    }

    // Constructor protegido sin parámetros requerido por el materializador de EF Core.
    private Cohorte()
    {
    }

    /* Actualiza la denominación y ciclo lectivo de la cohorte.
    Exige que la entidad permanezca activa para prevenir ediciones sobre comisiones cerradas. */
    public void ActualizarDatos(string nombre, string periodo)
    {
        if (!Activo)
            throw new DomainException("No es posible modificar los datos de una cohorte inactiva.");

        ValidarDatos(nombre, periodo);

        Nombre = nombre.Trim();
        Periodo = periodo.Trim();
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Reasigna el docente responsable de la comisión (ej. por reemplazo o licencia).
    public void ReasignarDocente(Guid nuevoDocenteId)
    {
        if (!Activo)
            throw new DomainException("No es posible reasignar el docente de una cohorte inactiva.");

        if (nuevoDocenteId == Guid.Empty)
            throw new DomainException("El nuevo docente asignado no puede ser un identificador vacío.");

        DocenteId = nuevoDocenteId;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Ejecuta la baja lógica de la cohorte (cierre de comisión).
    public void Desactivar()
    {
        if (!Activo)
            throw new DomainException("La cohorte ya se encuentra inactiva.");

        Activo = false;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Reactiva una cohorte previamente desactivada.
    public void Activar()
    {
        if (Activo)
            throw new DomainException("La cohorte ya se encuentra activa.");

        Activo = true;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Validador privado reutilizable para invariantes de texto y longitud.
    private static void ValidarDatos(string nombre, string periodo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre de la cohorte es obligatorio.");

        if (nombre.Trim().Length > 100)
            throw new DomainException("El nombre de la cohorte no puede superar los 100 caracteres.");

        if (string.IsNullOrWhiteSpace(periodo))
            throw new DomainException("El período lectivo es obligatorio.");

        if (periodo.Trim().Length > 30)
            throw new DomainException("El período lectivo no puede superar los 30 caracteres.");
    }
}
