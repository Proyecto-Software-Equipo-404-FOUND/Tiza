using PEI.BuildingBlocks.Common;

namespace PEI.IA.Domain.Entities;

/// <summary>
/// Representa una solicitud de corrección automatizada procesada por el servicio de IA.
/// Almacena tanto la petición como los resultados y métricas del procesamiento.
/// </summary>
public class SolicitudCorreccion : Entity<Guid>
{
    /// <summary>Institución a la que pertenece la evaluación corregida.</summary>
    public Guid InstitucionId { get; private set; }

    /// <summary>Identificador de la evaluación origen (referencia cruzada con microservicio Evaluaciones).</summary>
    public Guid? EvaluacionId { get; private set; }

    /// <summary>Identificador de la pregunta dentro de la evaluación.</summary>
    public Guid? PreguntaId { get; private set; }

    /// <summary>Texto de la respuesta del alumno a corregir.</summary>
    public string RespuestaTexto { get; private set; } = string.Empty;

    /// <summary>Enunciado de la pregunta.</summary>
    public string PreguntaEnunciado { get; private set; } = string.Empty;

    /// <summary>Rúbrica del docente serializada como JSON.</summary>
    public string RubricaJson { get; private set; } = string.Empty;

    /// <summary>Puntaje máximo posible según la rúbrica.</summary>
    public decimal PuntajeMaximo { get; private set; }

    // ── Resultados de la corrección ──

    /// <summary>Nota sugerida por el modelo de IA.</summary>
    public decimal? NotaSugerida { get; private set; }

    /// <summary>Justificación pedagógica generada por el modelo.</summary>
    public string? Justificacion { get; private set; }

    /// <summary>Desglose por criterio de la rúbrica (JSON).</summary>
    public string? DesglosePorCriterioJson { get; private set; }

    // ── Métricas de procesamiento ──

    /// <summary>Proveedor de IA utilizado (Groq, OpenRouter, Local, etc.).</summary>
    public string? Proveedor { get; private set; }

    /// <summary>Modelo específico utilizado (llama-3.3-70b, etc.).</summary>
    public string? Modelo { get; private set; }

    /// <summary>Cantidad de tokens de entrada consumidos.</summary>
    public int? TokensEntrada { get; private set; }

    /// <summary>Cantidad de tokens de salida generados.</summary>
    public int? TokensSalida { get; private set; }

    /// <summary>Latencia total de la llamada al LLM en milisegundos.</summary>
    public long? LatenciaMs { get; private set; }

    /// <summary>Costo estimado en USD de la llamada.</summary>
    public decimal? CostoEstimadoUsd { get; private set; }

    /// <summary>Estado actual de la solicitud.</summary>
    public EstadoCorreccion Estado { get; private set; } = EstadoCorreccion.Pendiente;

    /// <summary>Mensaje de error si la corrección falló.</summary>
    public string? Error { get; private set; }

    // ── Constructor ──

    private SolicitudCorreccion() { } // EF Core

    public static SolicitudCorreccion Crear(
        Guid institucionId,
        string respuestaTexto,
        string preguntaEnunciado,
        string rubricaJson,
        decimal puntajeMaximo,
        Guid? evaluacionId = null,
        Guid? preguntaId = null)
    {
        return new SolicitudCorreccion
        {
            Id = Guid.NewGuid(),
            InstitucionId = institucionId,
            RespuestaTexto = respuestaTexto,
            PreguntaEnunciado = preguntaEnunciado,
            RubricaJson = rubricaJson,
            PuntajeMaximo = puntajeMaximo,
            EvaluacionId = evaluacionId,
            PreguntaId = preguntaId,
            Estado = EstadoCorreccion.Pendiente
        };
    }

    /// <summary>Registra el resultado exitoso de la corrección.</summary>
    public void RegistrarResultado(
        decimal notaSugerida,
        string justificacion,
        string? desglosePorCriterioJson,
        string proveedor,
        string modelo,
        int tokensEntrada,
        int tokensSalida,
        long latenciaMs,
        decimal costoEstimadoUsd)
    {
        NotaSugerida = notaSugerida;
        Justificacion = justificacion;
        DesglosePorCriterioJson = desglosePorCriterioJson;
        Proveedor = proveedor;
        Modelo = modelo;
        TokensEntrada = tokensEntrada;
        TokensSalida = tokensSalida;
        LatenciaMs = latenciaMs;
        CostoEstimadoUsd = costoEstimadoUsd;
        Estado = EstadoCorreccion.Completada;
        FechaModificacionUtc = DateTime.UtcNow;
    }

    /// <summary>Registra un fallo en la corrección.</summary>
    public void RegistrarError(string error)
    {
        Error = error;
        Estado = EstadoCorreccion.Fallida;
        FechaModificacionUtc = DateTime.UtcNow;
    }
}

/// <summary>Estados posibles de una solicitud de corrección.</summary>
public enum EstadoCorreccion
{
    Pendiente,
    EnProceso,
    Completada,
    Fallida
}
