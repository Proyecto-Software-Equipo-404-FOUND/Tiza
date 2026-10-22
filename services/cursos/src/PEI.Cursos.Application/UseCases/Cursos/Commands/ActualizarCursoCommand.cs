using System;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Comando para modificar el nombre y area tematica de un curso perteneciente al tenant.
Combina el identificador de la URL con los campos descriptivos enviados en el body. */
public record ActualizarCursoCommand(
    Guid Id,
    string Nombre,
    string Area
);
