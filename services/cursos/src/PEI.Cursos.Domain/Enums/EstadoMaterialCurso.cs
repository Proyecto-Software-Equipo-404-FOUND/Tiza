namespace PEI.Cursos.Domain.Enums;

/* Estados posibles del ciclo de vida y procesamiento de un apunte de curso.
Se mapea en Entity Framework Core hacia una columna VARCHAR(30) en PostgreSQL ("Procesando", "Listo", "Error"). */
public enum EstadoMaterialCurso
{
    Procesando = 1,
    Listo = 2,
    Error = 3
}
