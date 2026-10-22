using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cohortes.Queries;

/* Manejador de la consulta ValidarCohorteQuery (Caso de Uso CU-05).
Consulta el repositorio de cohortes aplicando aislamiento multi-tenant y proyecta a CohorteValidacionDto. */
public class ValidarCohorteQueryHandler : IRequestHandler<ValidarCohorteQuery, CohorteValidacionDto?>
{
    private readonly ICohorteRepository _cohorteRepository;

    public ValidarCohorteQueryHandler(ICohorteRepository cohorteRepository)
    {
        _cohorteRepository = cohorteRepository;
    }

    public async Task<CohorteValidacionDto?> HandleAsync(ValidarCohorteQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Validacion preventiva de identificador (fail-fast)
        if (request.CohorteId == Guid.Empty)
        {
            return null;
        }

        // 2. Consulta al repositorio con filtro multi-tenant automatico
        var cohorte = await _cohorteRepository.GetByIdAsync(request.CohorteId, cancellationToken);
        if (cohorte is null)
        {
            return null;
        }

        // 3. Proyeccion al DTO inmutable de respuesta
        return new CohorteValidacionDto(
            cohorte.Id,
            cohorte.InstitucionId,
            cohorte.CursoId,
            cohorte.DocenteId,
            cohorte.Nombre,
            cohorte.Activo
        );
    }
}
