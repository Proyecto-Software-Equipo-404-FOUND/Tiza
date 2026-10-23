using System;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Comando inmutable para la actualizacion de datos de una cohorte o comision.
Encapsula los identificadores de ruta y los nuevos datos provistos en el request. */
public record ActualizarCohorteCommand(
    Guid CursoId,
    Guid Id,
    string Nombre,
    string Periodo,
    Guid DocenteId
);
