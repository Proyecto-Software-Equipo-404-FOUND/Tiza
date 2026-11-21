using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Enums;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Manejador de la consulta ObtenerTextoMaterialPublicoQuery (Caso de Uso S3-CU-03 / Issue #229).
Expone el texto del apunte para confirmacion previa del docente y consumo del frontend / IA.
Valida permisos de lectura del curso, integridad jerarquica del material y consistencia de estado. */
public class ObtenerTextoMaterialPublicoQueryHandler : IRequestHandler<ObtenerTextoMaterialPublicoQuery, TextoMaterialDto>
{
    private readonly IMaterialCursoRepository _materialRepository;
    private readonly IAutorizacionMaterialService _autorizacionService;

    public ObtenerTextoMaterialPublicoQueryHandler(
        IMaterialCursoRepository materialRepository,
        IAutorizacionMaterialService autorizacionService)
    {
        _materialRepository = materialRepository;
        _autorizacionService = autorizacionService;
    }

    public async Task<TextoMaterialDto> HandleAsync(ObtenerTextoMaterialPublicoQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Validar autorizacion de lectura sobre el curso (404 anti-enumeracion / 403 permisos)
        await _autorizacionService.ValidarPermisoLecturaAsync(request.CursoId, cancellationToken);

        // 2. Obtener el material aplicando el aislamiento multi-tenant del repositorio
        var material = await _materialRepository.GetByIdAsync(request.MaterialId, cancellationToken);

        // 3. Regla anti-enumeracion y consistencia jerarquica: el apunte debe existir, estar activo y pertenecer al curso
        if (material is null || !material.Activo || material.CursoId != request.CursoId)
        {
            throw new KeyNotFoundException($"El material con ID '{request.MaterialId}' no existe o no pertenece al curso especificado.");
        }

        // 4. Regla de consistencia: si el material no termino de procesarse o fallo la extraccion, emite 409 Conflict
        if (material.Estado != EstadoMaterialCurso.Listo)
        {
            throw new InvalidOperationException("El apunte todavía se está procesando.");
        }

        // 5. Determinar la presencia de contenido textual legible sin lanzar excepcion si es escaneado
        var tieneTexto = !string.IsNullOrWhiteSpace(material.TextoExtraido);
        var texto = tieneTexto ? material.TextoExtraido! : string.Empty;

        return new TextoMaterialDto(
            material.Id,
            material.Titulo,
            material.CantidadPaginas,
            texto,
            tieneTexto
        );
    }
}
