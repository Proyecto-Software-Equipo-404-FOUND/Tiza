using System.Text.RegularExpressions;
using PEI.IA.Application.DTOs;

namespace PEI.IA.Application.Guardrails;

/// <summary>
/// Implementación de barandas de seguridad pre y post inferencia.
/// Aplica filtros heurísticos y validaciones estrictas de dominio.
/// </summary>
public class BarandaSeguridad : IBarandaSeguridad
{
    // Patrones regex pre-compilados para detectar intentos típicos de manipulación e inyección de prompt
    private static readonly Regex[] PatronesSospechosos = new[]
    {
        // Directivas de ignorar rúbrica o instrucciones previas
        new Regex(@"\b(ignora|ignorá|olvida|olvidá|desestima)\s+(la\s+)?(rúbrica|rubrica|instrucciones|consigna|reglas)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(ignore|disregard|forget)\s+(all\s+)?(previous\s+)?(instructions|rules|rubric|prompt)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        
        // Peticiones directas de asignación de nota máxima o arbitraria
        new Regex(@"\b(poneme|ponéme|asigna|asigná|califica(me)?\s+con|dame)\s+(un\s+)?(10|diez|máxima\s+nota|maxima\s+nota|puntaje\s+máximo|puntaje\s+maximo|full\s+points)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(give\s+me|score|grade)\s+(a\s+)?(10|ten|maximum|full\s+marks)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Inyecciones de rol o delimitadores del sistema
        new Regex(@"\b(system\s*(prompt|override|message)|developer\s*mode|modo\s*desarrollador)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"<\/?(system|instruction|prompt|respuesta_estudiante)>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\[(INICIO|FIN)_RESPUESTA\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"```json\s*\{\s*""notaSugerida""", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public ResultadoSeguridadEntrada EvaluarEntrada(string? respuestaAlumno)
    {
        if (string.IsNullOrWhiteSpace(respuestaAlumno))
        {
            return new ResultadoSeguridadEntrada(false, null);
        }

        foreach (var patron in PatronesSospechosos)
        {
            var match = patron.Match(respuestaAlumno);
            if (match.Success)
            {
                return new ResultadoSeguridadEntrada(
                    EsSospechosa: true,
                    Motivo: $"Se detectó un patrón sospechoso de manipulación o inyección de prompt: '{match.Value}'."
                );
            }
        }

        return new ResultadoSeguridadEntrada(false, null);
    }

    public ResultadoValidacionSalida ValidarSalida(
        decimal? notaSugerida,
        decimal puntajeMaximo,
        string? justificacion,
        IReadOnlyList<DesgloseCriterio>? desglose)
    {
        if (!notaSugerida.HasValue)
        {
            return new ResultadoValidacionSalida(false, "La nota sugerida no puede ser nula.");
        }

        if (notaSugerida.Value < 0 || notaSugerida.Value > puntajeMaximo)
        {
            return new ResultadoValidacionSalida(
                false,
                $"La nota sugerida ({notaSugerida.Value}) se encuentra fuera del rango permitido [0, {puntajeMaximo}].");
        }

        if (string.IsNullOrWhiteSpace(justificacion))
        {
            return new ResultadoValidacionSalida(false, "La justificación pedagógica no puede estar vacía.");
        }

        if (desglose != null && desglose.Count > 0)
        {
            decimal sumaCriterios = 0;
            foreach (var item in desglose)
            {
                if (string.IsNullOrWhiteSpace(item.Criterio))
                {
                    return new ResultadoValidacionSalida(false, "El desglose contiene un criterio sin descripción.");
                }

                if (item.PuntajeOtorgado < 0)
                {
                    return new ResultadoValidacionSalida(
                        false,
                        $"El criterio '{item.Criterio}' tiene un puntaje otorgado negativo ({item.PuntajeOtorgado}).");
                }

                if (item.PuntajeMaximo > 0 && item.PuntajeOtorgado > item.PuntajeMaximo)
                {
                    return new ResultadoValidacionSalida(
                        false,
                        $"El criterio '{item.Criterio}' tiene un puntaje otorgado ({item.PuntajeOtorgado}) superior a su máximo ({item.PuntajeMaximo}).");
                }

                sumaCriterios += item.PuntajeOtorgado;
            }

            // Validar consistencia global de la suma frente a la nota total si se desglosaron criterios con topes
            if (desglose.Any(d => d.PuntajeMaximo > 0) && Math.Abs(sumaCriterios - notaSugerida.Value) > 0.15m)
            {
                return new ResultadoValidacionSalida(
                    false,
                    $"La suma de los puntajes por criterio ({sumaCriterios}) difiere significativamente de la nota sugerida global ({notaSugerida.Value}).");
            }
        }

        return new ResultadoValidacionSalida(true, null);
    }
}
