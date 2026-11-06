using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Enums;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Manejador de la consulta ObtenerTextoMaterialQuery.
Provee el texto completo extraido del documento para el consumo interno de IA / MCP.
Valida que el solicitante no sea alumno y que el material este activo y en estado Listo. */
public class ObtenerTextoMaterialQueryHandler : IRequestHandler<ObtenerTextoMaterialQuery, TextoMaterialDto>
{
    private readonly IMaterialCursoRepository _materialRepository;
    private readonly IAutorizacionMaterialService _autorizacionService;

    public ObtenerTextoMaterialQueryHandler(
        IMaterialCursoRepository materialRepository,
        IAutorizacionMaterialService autorizacionService)
    {
        _materialRepository = materialRepository;
        _autorizacionService = autorizacionService;
    }

    public async Task<TextoMaterialDto> HandleAsync(ObtenerTextoMaterialQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Validar que el usuario no sea Alumno (RN-MAT-02)
        _autorizacionService.ValidarAccesoTextoInterno();

        // 2. Obtener el material por ID aplicando el aislamiento multi-tenant
        var material = await _materialRepository.GetByIdAsync(request.Id, cancellationToken);

        // 3. Regla anti-enumeracion y consistencia: si no existe, esta inactivo o no esta Listo emite 404
        if (material is null || !material.Activo || material.Estado != EstadoMaterialCurso.Listo)
        {
            throw new KeyNotFoundException($"El material con ID '{request.Id}' no existe, fue dado de baja o no se encuentra listo.");
        }

        // 4. Proyeccion al DTO de texto
        return new TextoMaterialDto(
            material.Id,
            material.Titulo,
            material.CantidadPaginas,
            material.TextoExtraido
        );
    }
}
