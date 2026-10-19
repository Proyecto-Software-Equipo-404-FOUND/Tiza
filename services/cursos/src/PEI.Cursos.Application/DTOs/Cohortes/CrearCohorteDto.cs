using System;

namespace PEI.Cursos.Application.DTOs.Cohortes;

/* DTO de entrada para el cuerpo del request HTTP POST /api/v1/cursos/{cursoId}/cohortes.
Recibe los datos especificos de la comision. El CursoId se obtiene de la ruta de la peticion. */
public record CrearCohorteDto(
    string Nombre,
    string Periodo,
    Guid DocenteId
);
