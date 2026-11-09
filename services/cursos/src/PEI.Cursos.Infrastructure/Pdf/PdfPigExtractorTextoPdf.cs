using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;
using UglyToad.PdfPig;

namespace PEI.Cursos.Infrastructure.Pdf;

/* Adaptador para la extraccion de texto y analisis estructural de documentos PDF mediante PdfPig.
Aplica las reglas de negocio RN-MAT-03 (maximo 30 paginas) y RN-MAT-04 (texto seleccionable obligatorio sin OCR). */
public class PdfPigExtractorTextoPdf : IExtractorTextoPdf
{
    public Task<ResultadoExtraccionPdf> ExtraerTextoAsync(Stream streamPdf, CancellationToken cancellationToken = default)
    {
        if (streamPdf == null)
            throw new ArgumentNullException(nameof(streamPdf), "El stream del archivo PDF no puede ser nulo.");

        byte[] bytes;
        if (streamPdf is MemoryStream ms)
        {
            bytes = ms.ToArray();
        }
        else
        {
            if (streamPdf.CanSeek)
            {
                streamPdf.Position = 0;
            }
            using var buffer = new MemoryStream();
            streamPdf.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        PdfDocument document;
        try
        {
            document = PdfDocument.Open(bytes);
        }
        catch (Exception ex)
        {
            throw new UnsupportedMediaTypeException("El archivo suministrado no es un documento PDF valido o se encuentra danado.", ex);
        }

        using (document)
        {
            var cantidadPaginas = document.NumberOfPages;

            if (cantidadPaginas <= 0)
            {
                throw new DomainException("El documento debe contener al menos una pagina.");
            }

            if (cantidadPaginas > MaterialCurso.LimitePaginasMaximo)
            {
                throw new DomainException($"El PDF supera el limite permitido de {MaterialCurso.LimitePaginasMaximo} paginas (contiene {cantidadPaginas} paginas).");
            }

            var textoBuilder = new StringBuilder();
            var primeraPagina = true;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!primeraPagina)
                {
                    textoBuilder.Append("\n\n");
                }

                var textoPagina = page.Text;
                if (!string.IsNullOrWhiteSpace(textoPagina))
                {
                    textoBuilder.Append(textoPagina.Trim());
                }

                primeraPagina = false;
            }

            var textoExtraido = textoBuilder.ToString().Trim();

            // Regla RN-MAT-04: Si el documento no posee texto seleccionable (ej. documento escaneado como imagen)
            if (string.IsNullOrWhiteSpace(textoExtraido))
            {
                throw new DomainException("El PDF no tiene texto seleccionable. Subi un PDF generado desde un procesador de texto, no escaneado.");
            }

            return Task.FromResult(new ResultadoExtraccionPdf(cantidadPaginas, textoExtraido));
        }
    }
}
