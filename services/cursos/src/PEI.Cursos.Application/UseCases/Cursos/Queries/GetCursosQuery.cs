namespace PEI.Cursos.Application.UseCases.Cursos.Queries;

/* Consulta paginada del catalogo de cursos de la institucion.
Admite filtros opcionales por area tematica y busqueda de texto libre por nombre. */
public record GetCursosQuery(
    string? Area = null,
    string? Busqueda = null,
    int Pagina = 1,
    int TamanoPagina = 10
);
