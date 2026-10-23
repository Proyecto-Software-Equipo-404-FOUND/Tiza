using System;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Comando inmutable para solicitar la baja logica de una cohorte. */
public record DesactivarCohorteCommand(
    Guid CursoId,
    Guid Id
);
