using PEI.BuildingBlocks.Errors;

namespace PEI.Cursos.Application.UseCases.Matriculaciones;

/// <summary>
/// S3-CU-04 (#230): el alumno ya tiene una matrícula activa en otra comisión del mismo curso.
/// Según el contrato (cursos-v1.json, 409 ALUMNO_EN_OTRA_COMISION), cuando se conoce la comisión
/// actual se informan cohorteActualId y cohorteActualNombre para que el administrador sepa dónde darlo de baja.
/// </summary>
public sealed class AlumnoEnOtraComisionException : CodedConflictException
{
    public AlumnoEnOtraComisionException(Guid? cohorteActualId = null, string? cohorteActualNombre = null)
        : base("ALUMNO_EN_OTRA_COMISION", "El alumno ya está en otra comisión de este curso.")
    {
        Extensions["cohorteActualId"] = cohorteActualId;
        Extensions["cohorteActualNombre"] = cohorteActualNombre;
    }
}
