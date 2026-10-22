using System;

namespace PEI.Cursos.Application.UseCases.Cursos.Queries;

/* Consulta para obtener el detalle de un curso individual por su identificador unico.
Permite consultar la ficha de la materia y resolver la cabecera Location en la creacion. */
public record GetCursoByIdQuery(Guid Id);
