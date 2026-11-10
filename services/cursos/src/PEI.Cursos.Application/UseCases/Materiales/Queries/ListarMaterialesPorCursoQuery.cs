using System;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Consulta para obtener el catalogo de materiales activos de un curso (OpenAPI v1).
Omite el texto completo del documento para optimizar la respuesta de red. */
public record ListarMaterialesPorCursoQuery(Guid CursoId);
