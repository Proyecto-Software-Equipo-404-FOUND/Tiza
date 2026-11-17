using System.Globalization;
using System.Text.Json;

namespace PEI.Evaluaciones.Application.Evaluaciones;

/// <summary>
/// S3-EV-01: lee el desglose por criterio que guarda la IA.
/// La IA manda cada criterio como { criterio, puntajeMaximo, puntajeOtorgado, comentario },
/// mientras que el formato original del contrato era { criterio, puntaje, comentario }.
/// Se aceptan los dos (y cualquier combinación de mayúsculas) para que las
/// calificaciones ya guardadas sigan leyéndose.
/// </summary>
public static class DesgloseCriterioLector
{
    public static List<DesgloseCriterioDto>? Leer(string? desgloseJson)
    {
        if (string.IsNullOrWhiteSpace(desgloseJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(desgloseJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var lista = new List<DesgloseCriterioDto>();
            foreach (var elemento in doc.RootElement.EnumerateArray())
            {
                if (elemento.ValueKind != JsonValueKind.Object) continue;

                var criterio = LeerTexto(elemento, "criterio") ?? string.Empty;
                var comentario = LeerTexto(elemento, "comentario") ?? string.Empty;
                var puntaje = LeerNumero(elemento, "puntajeOtorgado") ?? LeerNumero(elemento, "puntaje") ?? 0m;
                var puntajeMaximo = LeerNumero(elemento, "puntajeMaximo");

                lista.Add(new DesgloseCriterioDto(criterio, puntaje, puntajeMaximo, comentario));
            }

            return lista;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Buscar(JsonElement objeto, string nombre, out JsonElement valor)
    {
        foreach (var propiedad in objeto.EnumerateObject())
        {
            if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase))
            {
                valor = propiedad.Value;
                return true;
            }
        }

        valor = default;
        return false;
    }

    private static string? LeerTexto(JsonElement objeto, string nombre) =>
        Buscar(objeto, nombre, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static decimal? LeerNumero(JsonElement objeto, string nombre)
    {
        if (!Buscar(objeto, nombre, out var valor)) return null;

        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(valor.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }
}
