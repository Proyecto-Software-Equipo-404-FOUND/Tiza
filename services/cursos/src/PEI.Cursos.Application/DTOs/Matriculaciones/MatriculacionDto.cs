using System;

namespace PEI.Cursos.Application.DTOs.Matriculaciones;

/* Contrato de salida inmutable para la entidad Matriculacion expuesto a la API.
Representa la confirmacion de inscripcion del alumno en la cohorte con su estado de cursada. */
public record MatriculacionDto(
    Guid Id,
    Guid InstitucionId,
    Guid CohorteId,
    Guid AlumnoId,
    DateTime FechaAlta,
    string Estado
);
