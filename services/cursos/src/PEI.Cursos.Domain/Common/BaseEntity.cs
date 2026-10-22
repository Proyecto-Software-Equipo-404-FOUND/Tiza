using System;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Domain.Common;

/* Clase base abstracta para todas las entidades del microservicio de Cursos.
Centraliza el identificador único (UUID v4), el discriminador multi-tenant (InstitucionId),
el control de concurrencia optimista (Version) y las marcas temporales de auditoría UTC.
Al residir en el núcleo de Dominio, preserva la pureza arquitectónica con cero dependencias externas. */
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    // Clave de aislamiento multi-tenant para garantizar que ningún registro sea expuesto a otra institución.
    public Guid InstitucionId { get; protected set; }

    // Token de concurrencia optimista (Criterio A07 de cátedra) para evitar sobreescrituras (409 Conflict).
    public int Version { get; set; } = 1;

    public DateTime FechaCreacionUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? FechaModificacionUtc { get; protected set; }

    // Permite fijar un identificador determinista, utilizado exclusivamente en escenarios de siembra de datos o pruebas.
    public void AsignarId(Guid id)
    {
        if (id == Guid.Empty)
            throw new DomainException("El identificador a asignar no puede ser un GUID vacío.");

        Id = id;
    }
}
