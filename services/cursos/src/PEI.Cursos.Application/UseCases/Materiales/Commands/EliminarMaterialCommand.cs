using System;

namespace PEI.Cursos.Application.UseCases.Materiales.Commands;

/* Comando para la baja logica de un apunte o material de curso (RN-MAT-05).
Recibe el identificador del curso y del material desde los parametros de ruta. */
public record EliminarMaterialCommand(
    Guid CursoId,
    Guid Id
);
