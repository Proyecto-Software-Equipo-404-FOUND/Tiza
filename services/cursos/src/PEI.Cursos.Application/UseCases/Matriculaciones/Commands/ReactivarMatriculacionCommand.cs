using System;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Comando para reactivar la matriculacion previamente dada de baja de un alumno en una cohorte.
Ambos identificadores se obtienen de los parametros de ruta en la URL. */
public record ReactivarMatriculacionCommand(
    Guid CohorteId,
    Guid Id
);
