using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

/// <summary>
/// S3-EV-02 (#223): reglas de permisos sobre los exámenes. Se controlan siempre en el backend.
/// - Solo el Docente crea, edita, elimina y publica, y solo sus exámenes.
/// - El Administrador ve todos los de su institución (solo lectura). El Alumno no accede.
/// - Los exámenes sin dueño (anteriores al Sprint 3) los ve y maneja cualquier docente de la institución.
/// - Un examen ajeno se responde como inexistente (404), para no revelar que existe.
/// </summary>
public static class AccesoEvaluaciones
{
    public const string RolDocente = "Docente";
    public const string RolAdministrador = "Administrador";

    public static bool PuedeCrear(string? rol) => string.Equals(rol, RolDocente, StringComparison.OrdinalIgnoreCase);

    public static bool PuedeLeer(string? rol) => string.Equals(rol, RolDocente, StringComparison.OrdinalIgnoreCase) || string.Equals(rol, RolAdministrador, StringComparison.OrdinalIgnoreCase);

    public static bool PuedeModificar(string? rol) => string.Equals(rol, RolDocente, StringComparison.OrdinalIgnoreCase);

    /// <summary>docenteId null = sin filtro (Administrador).</summary>
    public static bool PuedeVer(Evaluacion evaluacion, Guid? docenteId) =>
        docenteId is null || evaluacion.DocenteId is null || evaluacion.DocenteId == docenteId;

    /// <summary>Filtro por docente según el rol: el Docente ve lo suyo, el Administrador todo.</summary>
    public static Guid? FiltroDocente(string? rol, Guid usuarioId) => string.Equals(rol, RolDocente, StringComparison.OrdinalIgnoreCase) ? usuarioId : null;
}

/// <summary>
/// Comprueba que el examen exista en la institución y que el usuario pueda verlo.
/// Lo usa el controlador antes de editar, eliminar, publicar o ver resultados.
/// </summary>
public record ComprobarAccesoEvaluacionQuery(Guid EvaluacionId, Guid InstitucionId, Guid? DocenteId)
    : IRequest<ResultadoOperacion<bool>>;

public class ComprobarAccesoEvaluacionHandler
    : IRequestHandler<ComprobarAccesoEvaluacionQuery, ResultadoOperacion<bool>>
{
    private readonly IEvaluacionRepositorio _repositorio;

    public ComprobarAccesoEvaluacionHandler(IEvaluacionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<bool>> Handle(ComprobarAccesoEvaluacionQuery request, CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerPorIdAsync(request.EvaluacionId, request.InstitucionId, ct);

        if (evaluacion is null || !AccesoEvaluaciones.PuedeVer(evaluacion, request.DocenteId))
        {
            return ResultadoOperacion<bool>.NoEncontrado();
        }

        return ResultadoOperacion<bool>.Ok(true);
    }
}
