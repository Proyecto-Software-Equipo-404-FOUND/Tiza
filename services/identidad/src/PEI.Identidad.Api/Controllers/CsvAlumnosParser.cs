using System.Text;
using PEI.Identidad.Application.UseCases.Usuarios.Importacion;

namespace PEI.Identidad.Api.Controllers;

public sealed class CsvImportFormatException(string message) : Exception(message);

public static class CsvAlumnosParser
{
    public const int MaximoBytes = 1024 * 1024;
    public const int MaximoFilas = 200;
    private static readonly UTF8Encoding Utf8Estricto = new(false, true);

    public static IReadOnlyList<FilaAlumnoCsv> Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) throw new CsvImportFormatException("El archivo CSV está vacío.");
        if (bytes.Length > MaximoBytes) throw new CsvImportFormatException("El archivo supera el máximo de 1 MiB.");

        string text;
        try { text = Utf8Estricto.GetString(bytes); }
        catch (DecoderFallbackException) { throw new CsvImportFormatException("El archivo debe estar codificado en UTF-8."); }
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

        var records = ParseRecords(text);
        if (records.Count == 0 || records[0].Count != 3 ||
            records[0][0] != "nombre" || records[0][1] != "apellido" || records[0][2] != "email")
            throw new CsvImportFormatException("El encabezado debe ser exactamente nombre,apellido,email.");
        if (records.Count - 1 > MaximoFilas) throw new CsvImportFormatException("El archivo supera el máximo de 200 filas de datos.");

        var filas = new List<FilaAlumnoCsv>(records.Count - 1);
        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Count != 3)
                throw new CsvImportFormatException($"La fila CSV {index + 1} no contiene exactamente tres columnas.");
            filas.Add(new(index + 1, record[0], record[1], record[2]));
        }
        return filas;
    }

    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var closedQuote = false;
        var endedWithRecordBreak = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            endedWithRecordBreak = false;
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index++; }
                    else { inQuotes = false; closedQuote = true; }
                }
                else field.Append(current);
                continue;
            }

            if (closedQuote && current is not ',' and not '\r' and not '\n')
                throw new CsvImportFormatException("El CSV contiene caracteres después del cierre de un campo entre comillas.");
            if (current == '"')
            {
                if (field.Length != 0 || closedQuote)
                    throw new CsvImportFormatException("El CSV contiene comillas fuera de lugar.");
                inQuotes = true;
            }
            else if (current == ',')
            {
                record.Add(field.ToString()); field.Clear(); closedQuote = false;
            }
            else if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                record.Add(field.ToString()); field.Clear(); closedQuote = false;
                records.Add(record); record = new List<string>(); endedWithRecordBreak = true;
            }
            else field.Append(current);
        }

        if (inQuotes) throw new CsvImportFormatException("El CSV contiene un campo entre comillas sin cerrar.");
        if (!endedWithRecordBreak || record.Count > 0 || field.Length > 0 || closedQuote)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
