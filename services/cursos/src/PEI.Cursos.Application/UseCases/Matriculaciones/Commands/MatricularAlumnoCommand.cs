using System;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Comando para matricular formalmente a un alumno en una cohorte especifica.
El cohorteId proviene de la ruta URL y el alumnoId del cuerpo del request. */
public record MatricularAlumnoCommand(
    Guid CohorteId,
    Guid AlumnoId
);
