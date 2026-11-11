using System.Text.Json;

namespace PEI.Evaluaciones.Application.Correccion;

// S2-EV-02 (#144): lectura de PreguntasJson (contrato preguntas-v1) y de RespuestasJson
// de una entrega, con lo justo para corregir preguntas abiertas.
public sealed record PreguntaAbierta(Guid Id, string Enunciado, decimal PuntajeMaximo, string RubricaJson);

public sealed record RespuestaGuardada(Guid PreguntaId, string Respuesta, int Secuencia);

public static class PreguntasCorreccion
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Dictionary<Guid, PreguntaAbierta> LeerAbiertas(string preguntasJson)
    {
        var resultado = new Dictionary<Guid, PreguntaAbierta>();
        using var documento = JsonDocument.Parse(preguntasJson);
        if (!documento.RootElement.TryGetProperty("preguntas", out var preguntas)
            || preguntas.ValueKind != JsonValueKind.Array)
        {
            return resultado;
        }

        foreach (var pregunta in preguntas.EnumerateArray())
        {
            if (pregunta.GetProperty("tipo").GetString() != "Abierta") continue;

            var id = pregunta.GetProperty("id").GetGuid();
            var puntajeMaximo = pregunta.GetProperty("puntajeMaximo").GetDecimal();
            var rubrica = pregunta.TryGetProperty("rubrica", out var r) && r.ValueKind == JsonValueKind.Object
                ? r.GetRawText()
                : RubricaPorDefecto(puntajeMaximo);

            resultado[id] = new PreguntaAbierta(
                id,
                pregunta.GetProperty("enunciado").GetString() ?? string.Empty,
                puntajeMaximo,
                rubrica);
        }

        return resultado;
    }

    // Una respuesta por pregunta: si hay varias, vale la de mayor secuencia (RN-COR-02).
    public static Dictionary<Guid, RespuestaGuardada> LeerUltimasRespuestas(string respuestasJson)
    {
        var respuestas = JsonSerializer.Deserialize<List<RespuestaGuardada>>(respuestasJson, Web) ?? new();
        return respuestas
            .Where(r => r.PreguntaId != Guid.Empty && !string.IsNullOrWhiteSpace(r.Respuesta))
            .GroupBy(r => r.PreguntaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Secuencia).First());
    }

    // Preguntas abiertas que el alumno respondió: son las que hay que corregir con IA.
    public static List<(PreguntaAbierta Pregunta, string Respuesta)> AbiertasRespondidas(
        string preguntasJson, string respuestasJson)
    {
        var abiertas = LeerAbiertas(preguntasJson);
        var respuestas = LeerUltimasRespuestas(respuestasJson);
        return abiertas.Values
            .Where(p => respuestas.ContainsKey(p.Id))
            .Select(p => (p, respuestas[p.Id].Respuesta))
            .ToList();
    }

    private static string RubricaPorDefecto(decimal puntajeMaximo) =>
        JsonSerializer.Serialize(new
        {
            criterios = new[] { new { descripcion = "Respuesta correcta, completa y fundamentada", puntaje = puntajeMaximo } }
        });
}
