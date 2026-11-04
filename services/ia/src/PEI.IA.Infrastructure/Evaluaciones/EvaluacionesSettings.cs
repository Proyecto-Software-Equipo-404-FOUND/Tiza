namespace PEI.IA.Infrastructure.Evaluaciones;

/// <summary>
/// Configuración del cliente HTTP hacia el microservicio de Evaluaciones.
/// Se lee de la sección "EvaluacionesSettings" en appsettings.json.
/// </summary>
public class EvaluacionesSettings
{
    /// <summary>URL base del servicio de Evaluaciones (sin ruta, sin slash final).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5103";

    /// <summary>Timeout en milisegundos para las llamadas HTTP (default: 10 s).</summary>
    public int TimeoutMs { get; set; } = 10_000;

    /// <summary>
    /// Si es false, el cliente retorna true inmediatamente sin llamar a Evaluaciones.
    /// Útil para desarrollo local cuando S2-EV-02 aún no está disponible.
    /// </summary>
    public bool HabilitarIntegracion { get; set; } = true;
}
