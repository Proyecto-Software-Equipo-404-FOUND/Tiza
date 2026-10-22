namespace PEI.Cursos.Application.DTOs.Cursos;

/* DTO para la actualizacion de datos descriptivos de un curso existente.
Recibe exclusivamente los campos mutables sin permitir alterar identificadores de tenant. */
public record ActualizarCursoDto(
    string Nombre,
    string Area
);
