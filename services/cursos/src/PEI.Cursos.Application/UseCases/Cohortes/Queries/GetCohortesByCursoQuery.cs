using System;

namespace PEI.Cursos.Application.UseCases.Cohortes.Queries;

/* Consulta para obtener todas las cohortes asociadas a un curso determinado.
Permite consultar las divisiones activas e historicas bajo una materia curricular. */
public record GetCohortesByCursoQuery(Guid CursoId);
