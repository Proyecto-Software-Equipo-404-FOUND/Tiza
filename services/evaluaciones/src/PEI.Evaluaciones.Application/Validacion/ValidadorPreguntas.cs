using System.Text.Json;
using Json.Schema;

namespace PEI.Evaluaciones.Application.Validacion;

public class ValidadorPreguntas
{
    private static readonly JsonSchema Esquema = CargarEsquema();

    private static readonly EvaluationOptions Opciones = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true
    };

    public ResultadoValidacion Validar(string preguntasJson)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(preguntasJson);
        }
        catch (JsonException)
        {
            return ResultadoValidacion.Invalido(new[] { "El texto de las preguntas no es un JSON valido." });
        }

        using (documento)
        {
            var resultado = Esquema.Evaluate(documento.RootElement, Opciones);

            if (resultado.IsValid)
            {
                return ResultadoValidacion.Valido();
            }

                        var errores = new List<string>();

            if (resultado.Errors is not null)
            {
                errores.AddRange(resultado.Errors.Select(e => $"{resultado.InstanceLocation}: {e.Value}"));
            }

            if (resultado.Details is not null)
            {
                foreach (var detalle in resultado.Details)
                {
                    if (detalle.Errors is not null)
                    {
                        errores.AddRange(detalle.Errors.Select(e => $"{detalle.InstanceLocation}: {e.Value}"));
                    }
                }
            }

            return ResultadoValidacion.Invalido(errores);
        }
    }

    private static JsonSchema CargarEsquema()
    {
        var assembly = typeof(ValidadorPreguntas).Assembly;
        using var stream = assembly.GetManifestResourceStream("preguntas-v1.schema.json");

        if (stream is null)
        {
            throw new InvalidOperationException("No se encontro el esquema preguntas-v1.schema.json embebido.");
        }

        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}