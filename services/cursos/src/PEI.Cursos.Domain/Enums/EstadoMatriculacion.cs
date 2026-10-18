namespace PEI.Cursos.Domain.Enums;

/* Estados posibles del ciclo de vida de una matriculación.
Se mapea en Entity Framework Core hacia la columna VARCHAR(30) en PostgreSQL ("Activa", "Baja", "Completada"). */
public enum EstadoMatriculacion
{
    Activa = 1,
    Baja = 2,
    Completada = 3
}
