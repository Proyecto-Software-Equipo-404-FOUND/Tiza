using PEI.BuildingBlocks.Errors;

namespace PEI.Cursos.Application.UseCases.Cursos;

/* Excepción de conflicto de negocio lanzada al intentar desactivar un curso
que todavía posee alumnos con matrícula activa en cualquiera de sus comisiones. */
public sealed class CursoConAlumnosException()
    : CodedConflictException("CURSO_CON_ALUMNOS", "No se puede dar de baja el curso porque tiene alumnos con matrícula activa en sus comisiones. Dalos de baja primero.");
