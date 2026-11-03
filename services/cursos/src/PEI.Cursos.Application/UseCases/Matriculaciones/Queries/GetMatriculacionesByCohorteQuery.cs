using System;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

/* Consulta CQRS para listar las matriculaciones asociadas a una cohorte.
Admite un filtro opcional para recuperar exclusivamente matriculaciones activas. */
public record GetMatriculacionesByCohorteQuery(
    Guid CohorteId,
    bool? SoloActivas = true
);
