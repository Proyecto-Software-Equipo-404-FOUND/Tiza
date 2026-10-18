using System;
using System.Collections.Generic;
using PEI.Cursos.Domain.Common;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Domain.Entities;

/* Entidad de Dominio rica que representa una materia o espacio curricular.
Encapsula sus atributos impidiendo mutaciones externas directas (private set)
y garantiza que sus invariantes de negocio se cumplan en todo momento. */
public class Curso : BaseEntity
{
    public string Nombre { get; private set; } = null!;
    public string Area { get; private set; } = null!;
    public bool Activo { get; private set; }

    /* Colección de navegación hacia las comisiones asociadas al curso.
    Es virtual para soportar lazy loading y se inicializa vacía para prevenir NullReferenceException. */
    public virtual ICollection<Cohorte> Cohortes { get; private set; } = new List<Cohorte>();

    /* Constructor de negocio. Exige los datos obligatorios e impide instanciar
    un curso en estado inconsistente o sin tenant asignado. */
    public Curso(Guid institucionId, string nombre, string area)
    {
        if (institucionId == Guid.Empty)
            throw new DomainException("La institución es obligatoria para registrar un curso.");

        ValidarDatos(nombre, area);

        InstitucionId = institucionId;
        Nombre = nombre.Trim();
        Area = area.Trim();
        Activo = true;
    }

    // Constructor sin parámetros requerido exclusivamente por EF Core para materialización.
    private Curso()
    {
    }

    /* Actualiza la información descriptiva del curso garantizando que
    la entidad permanezca activa y con datos conformes a las invariantes. */
    public void ActualizarDatos(string nombre, string area)
    {
        if (!Activo)
            throw new DomainException("No es posible modificar los datos de un curso inactivo.");

        ValidarDatos(nombre, area);

        Nombre = nombre.Trim();
        Area = area.Trim();
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Ejecuta la baja lógica del curso impidiendo la apertura de nuevas cohortes.
    public void Desactivar()
    {
        if (!Activo)
            throw new DomainException("El curso ya se encuentra inactivo.");

        Activo = false;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Reactiva un curso previamente dado de baja lógica.
    public void Activar()
    {
        if (Activo)
            throw new DomainException("El curso ya se encuentra activo.");

        Activo = true;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    // Validador privado reutilizable para garantizar las invariantes de texto y longitud.
    private static void ValidarDatos(string nombre, string area)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre del curso es obligatorio.");

        if (nombre.Trim().Length > 150)
            throw new DomainException("El nombre del curso no puede superar los 150 caracteres.");

        if (string.IsNullOrWhiteSpace(area))
            throw new DomainException("El área temática del curso es obligatoria.");

        if (area.Trim().Length > 50)
            throw new DomainException("El área temática no puede superar los 50 caracteres.");
    }
}
