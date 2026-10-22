namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Constructor de prompts para las solicitudes de corrección pedagógica de IA.
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Obtiene el system prompt pedagógico configurado.
    /// </summary>
    string BuildSystemPrompt();

    /// <summary>
    /// Construye el prompt de usuario sustituyendo los datos de la consigna, rúbrica y respuesta del alumno.
    /// </summary>
    string BuildUserPrompt(string enunciado, string rubricaJson, decimal puntajeMaximo, string respuestaAlumno);
}
