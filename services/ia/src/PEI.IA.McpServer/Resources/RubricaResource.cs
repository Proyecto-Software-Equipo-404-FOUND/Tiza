namespace PEI.IA.McpServer.Resources;

/// <summary>
/// Proveedor del recurso MCP que expone la rúbrica de una evaluación.
/// El LLM puede solicitar este recurso como contexto adicional antes de calificar.
/// URI: rubrica://{evaluacionId}
/// </summary>
public static class RubricaResource
{
    public const string UriScheme = "rubrica";
    public const string UriTemplate = "rubrica://{evaluacionId}";

    /// <summary>
    /// Obtiene la rúbrica correspondiente al evaluacionId especificado.
    /// Actualmente sirve la rúbrica del dataset local de corrección pedagógica;
    /// la integración remota con el microservicio de Evaluaciones se extenderá en el Sprint 3.
    /// </summary>
    public static string ObtenerRubrica(string evaluacionId)
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "datasets", "dataset-correccion-v1.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "datasets", "dataset-correccion-v1.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "datasets", "dataset-correccion-v1.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "services", "ia", "datasets", "dataset-correccion-v1.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "datasets", "dataset-correccion-v1.json")
        };

        var found = possiblePaths.FirstOrDefault(File.Exists);
        if (found == null)
            return $"{{\"error\": \"Dataset no encontrado\", \"evaluacionId\": \"{evaluacionId}\"}}";

        return File.ReadAllText(found);
    }
}
