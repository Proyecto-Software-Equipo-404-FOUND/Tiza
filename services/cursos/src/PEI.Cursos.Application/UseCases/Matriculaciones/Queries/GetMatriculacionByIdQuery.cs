using System;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

/* Consulta del detalle individual de una matriculacion por su identificador.
Soporta la resolucion de la cabecera Location en el estandar REST Nivel 2. */
public record GetMatriculacionByIdQuery(
    Guid Id
);
