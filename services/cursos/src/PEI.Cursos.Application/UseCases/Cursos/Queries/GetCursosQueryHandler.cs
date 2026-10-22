using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Common.DTOs;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Queries;

/* Manejador de la consulta GetCursosQuery.
Recupera cursos paginados desde el repositorio y los envuelve en el contenedor generico ResultadoPaginadoDTO. */
public class GetCursosQueryHandler : IRequestHandler<GetCursosQuery, ResultadoPaginadoDTO<CursoDto>>
{
    private readonly ICursoRepository _cursoRepository;

    public GetCursosQueryHandler(ICursoRepository cursoRepository)
    {
        _cursoRepository = cursoRepository;
    }

    public async Task<ResultadoPaginadoDTO<CursoDto>> HandleAsync(GetCursosQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener cursos paginados y conteo total desde el repositorio filtrado por tenant
        var (cursos, totalItems) = await _cursoRepository.GetPaginadosAsync(
            request.Area,
            request.Busqueda,
            request.Pagina,
            request.TamanoPagina,
            cancellationToken
        );

        // 2. Proyectar entidades de dominio a DTOs de salida inmutables
        var itemsDto = cursos.Select(c => new CursoDto(
            c.Id,
            c.InstitucionId,
            c.Nombre,
            c.Area,
            c.Activo,
            c.FechaCreacionUtc
        )).ToList();

        // 3. Calcular la cantidad total de paginas disponibles
        var tamanoSeguro = request.TamanoPagina <= 0 ? 10 : request.TamanoPagina;
        var totalPaginas = (int)Math.Ceiling((double)totalItems / tamanoSeguro);

        // 4. Retornar el contenedor enriquecido con elementos y metadata de navegacion
        return new ResultadoPaginadoDTO<CursoDto>(
            Items: itemsDto,
            TotalItems: totalItems,
            Pagina: request.Pagina < 1 ? 1 : request.Pagina,
            TamanoPagina: tamanoSeguro,
            TotalPaginas: totalPaginas
        );
    }
}
