using System;

namespace PEI.Cursos.Application.UseCases.Materiales.Queries;

/* Consulta para obtener el texto completo extraido de un apunte (recurso interno consumido por IA). */
public record ObtenerTextoMaterialQuery(Guid Id);
