using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Manejador de la consulta ListarMaterialesPorCursoQuery.
Verifica permisos (RN-MAT-01 / RN-MAT-02) y retorna la coleccion de materiales activos ordenados cronologicamente. */
public class ListarMaterialesPorCursoQueryHandler : IRequestHandler<ListarMaterialesPorCursoQuery, IReadOnlyList<MaterialCursoDto>>
{
    private readonly IMaterialCursoRepository _materialRepository;
    private readonly IAutorizacionMaterialService _autorizacionService;

    public ListarMaterialesPorCursoQueryHandler(
        IMaterialCursoRepository materialRepository,
        IAutorizacionMaterialService autorizacionService)
    {
        _materialRepository = materialRepository;
        _autorizacionService = autorizacionService;
    }

    public async Task<IReadOnlyList<MaterialCursoDto>> HandleAsync(ListarMaterialesPorCursoQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Validar que el curso exista en el tenant (404) y que el usuario tenga permisos de lectura (403)
        await _autorizacionService.ValidarPermisoLecturaAsync(request.CursoId, cancellationToken);

        // 2. Obtener materiales activos ordenados por fecha descendente
        var materiales = await _materialRepository.GetByCursoIdAsync(request.CursoId, cancellationToken);

        // 3. Proyectar a DTOs inmutables
        return materiales.Select(m => new MaterialCursoDto(
            m.Id,
            m.InstitucionId,
            m.CursoId,
            m.Titulo,
            m.NombreArchivo,
            m.TamanoBytes,
            m.CantidadPaginas,
            m.Estado.ToString(),
            m.SubidoPorId,
            m.FechaSubidaUtc
        )).ToList();
    }
}
