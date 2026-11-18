using PEI.BuildingBlocks.Errors;

namespace PEI.Cursos.Application.UseCases.Matriculaciones;

public sealed class AlumnoEnOtraComisionException()
    : CodedConflictException("ALUMNO_EN_OTRA_COMISION", "El alumno ya está en otra comisión de este curso.");
