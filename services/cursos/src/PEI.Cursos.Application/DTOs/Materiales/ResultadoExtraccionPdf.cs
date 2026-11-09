namespace PEI.Cursos.Application.DTOs.Materiales;

/* Resultado inmutable de la extraccion y validacion de un documento PDF.
Contiene el conteo total de paginas y el texto completo procesado con salto de linea doble entre paginas. */
public record ResultadoExtraccionPdf(int CantidadPaginas, string TextoExtraido);
