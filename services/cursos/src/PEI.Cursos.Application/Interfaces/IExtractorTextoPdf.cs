using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Materiales;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato para la extraccion de texto y analisis de paginas de documentos PDF (ISP y DIP).
Encapsula el uso de librerias de procesamiento de documentos (PdfPig) aislando el resto de la aplicacion. */
public interface IExtractorTextoPdf
{
    // Procesa el stream del PDF, valida limites de paginas y extrae el texto seleccionable.
    Task<ResultadoExtraccionPdf> ExtraerTextoAsync(Stream streamPdf, CancellationToken cancellationToken = default);
}
