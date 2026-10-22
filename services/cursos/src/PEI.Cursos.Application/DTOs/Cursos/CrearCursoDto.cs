namespace PEI.Cursos.Application.DTOs.Cursos;

/* DTO de entrada para el cuerpo del request HTTP POST /api/v1/cursos.
Recibe exclusivamente los datos requeridos por el cliente sin admitir manipulacion de tenant. */
public record CrearCursoDto(
    string Nombre,
    string Area
);
