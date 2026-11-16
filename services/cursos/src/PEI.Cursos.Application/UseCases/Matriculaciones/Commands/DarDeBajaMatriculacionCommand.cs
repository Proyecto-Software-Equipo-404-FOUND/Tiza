using System;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Comando para dar de baja la matriculacion de un alumno en una cohorte especifica.
Ambos identificadores se obtienen de los parametros de ruta en la URL. */
public record DarDeBajaMatriculacionCommand(
    Guid CohorteId,
    Guid Id
);
