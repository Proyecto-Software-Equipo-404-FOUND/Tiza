namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Abstracción para la ejecución de herramientas (tools).
/// Permite cambiar entre ejecución via MCP (real) o ejecución local (Plan B).
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Ejecuta una herramienta por nombre con los argumentos proporcionados.
    /// </summary>
    /// <param name="toolName">Nombre de la herramienta a ejecutar.</param>
    /// <param name="argumentosJson">Argumentos serializados como JSON.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado de la ejecución serializado como JSON.</returns>
    Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las herramientas disponibles.
    /// </summary>
    /// <returns>Lista de definiciones de tools disponibles.</returns>
    Task<IReadOnlyList<LlmToolDefinition>> ListarToolsAsync(CancellationToken cancellationToken = default);
}
