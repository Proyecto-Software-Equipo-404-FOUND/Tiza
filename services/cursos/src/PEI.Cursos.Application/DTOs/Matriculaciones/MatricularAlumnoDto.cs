using System;

namespace PEI.Cursos.Application.DTOs.Matriculaciones;

/* DTO de entrada para el cuerpo del request HTTP POST /api/v1/cohortes/{cohorteId}/matriculas.
Recibe el identificador del alumno a matricular. El cohorteId se extrae de la ruta URL. */
public record MatricularAlumnoDto(
    Guid AlumnoId
);
