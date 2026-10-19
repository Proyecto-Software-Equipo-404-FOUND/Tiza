using System.Collections.Generic;

namespace PEI.Cursos.Application.Common.DTOs;

/* Contenedor generico inmutable para respuestas paginadas de la API (RESTful Nivel 2).
Encapsula los elementos solicitados junto con metadatos de navegacion
para que los clientes y el Frontend conozcan el estado de paginacion. */
public record ResultadoPaginadoDTO<T>(
    IReadOnlyList<T> Items,
    int TotalItems,
    int Pagina,
    int TamanoPagina,
    int TotalPaginas
)
{
    // Banderas calculadas para simplificar los controles de navegacion en el cliente
    public bool TienePaginaPrevia => Pagina > 1;
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;
}
