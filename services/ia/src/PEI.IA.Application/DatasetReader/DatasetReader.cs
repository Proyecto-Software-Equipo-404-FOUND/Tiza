using System.Text.Json;

namespace PEI.IA.Application.DatasetReader;

/// <summary>
/// Lector y parser del dataset JSON de consignas, rúbricas y respuestas de evaluación.
/// Facilita la inyección de rúbricas al contexto del modelo y la ejecución de benchmarks.
/// </summary>
public class DatasetReader
{
    private DatasetCorreccion? _dataset;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void CargarDesdeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        _dataset = JsonSerializer.Deserialize<DatasetCorreccion>(json, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo deserializar el contenido del dataset JSON.");
    }

    public async Task CargarDesdeArchivoAsync(string rutaArchivo, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaArchivo))
        {
            throw new FileNotFoundException($"No se encontró el archivo del dataset en la ruta: {rutaArchivo}");
        }

        var json = await File.ReadAllTextAsync(rutaArchivo, cancellationToken);
        CargarDesdeJson(json);
    }

    public Task<IReadOnlyList<PreguntaDataset>> ObtenerPreguntasAsync(CancellationToken cancellationToken = default)
    {
        AsegurarCargado();
        return Task.FromResult<IReadOnlyList<PreguntaDataset>>(_dataset!.Preguntas);
    }

    public Task<PreguntaDataset?> ObtenerPreguntaPorIdAsync(string id, CancellationToken cancellationToken = default)
    {
        AsegurarCargado();
        var pregunta = _dataset!.Preguntas.FirstOrDefault(p =>
            p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(pregunta);
    }

    public Task<IReadOnlyList<RespuestaDataset>> ObtenerRespuestasPorPreguntaAsync(string preguntaId, CancellationToken cancellationToken = default)
    {
        AsegurarCargado();
        var pregunta = _dataset!.Preguntas.FirstOrDefault(p =>
            p.Id.Equals(preguntaId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<IReadOnlyList<RespuestaDataset>>(pregunta?.Respuestas ?? new List<RespuestaDataset>());
    }

    public string ObtenerRubricaJson(string preguntaId)
    {
        AsegurarCargado();
        var pregunta = _dataset!.Preguntas.FirstOrDefault(p =>
            p.Id.Equals(preguntaId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No se encontró la pregunta con ID: {preguntaId}");

        return JsonSerializer.Serialize(pregunta.Rubrica, JsonOptions);
    }

    private void AsegurarCargado()
    {
        if (_dataset is null)
        {
            throw new InvalidOperationException("El dataset no ha sido cargado. Llame a CargarDesdeJson() o CargarDesdeArchivoAsync() previamente.");
        }
    }
}
