namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Comando para la creacion de un nuevo curso academico.
Encapsula los datos requeridos provistos por el cliente en el cuerpo de la peticion. */
public record CrearCursoCommand(
    string Nombre,
    string Area
);
