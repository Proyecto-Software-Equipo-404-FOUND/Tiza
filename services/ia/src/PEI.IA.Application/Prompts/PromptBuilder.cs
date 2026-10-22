using System.Globalization;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Application.Prompts;

/// <summary>
/// Implementación predeterminada del constructor de prompts pedagógicos basada en la versión v1.
/// </summary>
public class PromptBuilder : IPromptBuilder
{
    public string BuildSystemPrompt()
    {
        return PromptCorreccionV1.SystemPrompt;
    }

    public string BuildUserPrompt(
        string enunciado,
        string rubricaJson,
        decimal puntajeMaximo,
        string respuestaAlumno)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enunciado);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubricaJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(respuestaAlumno);

        return PromptCorreccionV1.UserPromptTemplate
            .Replace("{ENUNCIADO}", enunciado.Trim())
            .Replace("{RUBRICA_JSON}", rubricaJson.Trim())
            .Replace("{PUNTAJE_MAXIMO}", puntajeMaximo.ToString("0.##", CultureInfo.InvariantCulture))
            .Replace("{RESPUESTA_ALUMNO}", respuestaAlumno.Trim());
    }
}
