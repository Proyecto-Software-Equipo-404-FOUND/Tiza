using System.Net;

namespace PEI.IA.Infrastructure.LlmClients;

/// <summary>
/// Opciones de resiliencia y configuración de Circuit Breaker para proveedores LLM.
/// </summary>
public class LlmResilienceOptions
{
    /// <summary>
    /// Cantidad de fallos consecutivos antes de que el Circuit Breaker de un proveedor se abra.
    /// Por defecto 2 fallos consecutivos.
    /// </summary>
    public int FallosConsecutivosParaAbrirCircuito { get; set; } = 2;

    /// <summary>
    /// Tiempo en segundos durante el cual el circuito permanece abierto antes de permitir una prueba (Half-Open).
    /// Por defecto 30 segundos.
    /// </summary>
    public int DuracionAperturaCircuitoSegundos { get; set; } = 30;

    /// <summary>
    /// Determina si una excepción ocurrida al llamar a un proveedor LLM debe desencadenar
    /// la conmutación al siguiente proveedor (ej. HTTP 429, 5xx, timeout o desconexión).
    /// </summary>
    public static bool EsFalloConmutable(Exception ex)
    {
        if (ex is TaskCanceledException or TimeoutException)
            return true;

        if (ex is HttpRequestException httpEx)
        {
            // Errores de red / conexión rechazada
            if (httpEx.StatusCode is null)
                return true;

            // 429 Too Many Requests o 5xx Server Errors
            return httpEx.StatusCode == HttpStatusCode.TooManyRequests
                || ((int)httpEx.StatusCode >= 500 && (int)httpEx.StatusCode <= 599);
        }

        // Si el mensaje indica 429 o timeout en excepciones envueltas
        var msg = ex.Message;
        if (msg.Contains("429") || msg.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
