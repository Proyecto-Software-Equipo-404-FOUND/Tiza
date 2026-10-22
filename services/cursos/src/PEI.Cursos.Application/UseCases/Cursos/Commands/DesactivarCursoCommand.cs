using System;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Comando para ejecutar la baja logica de un curso impidiendo la apertura de nuevas comisiones. */
public record DesactivarCursoCommand(Guid Id);
