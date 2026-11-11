namespace PEI.Evaluaciones.Application.Correccion;

// S2-EV-02 (#144): pedido de corrección a IA (POST /api/v1/Correcciones).
// Nunca lleva datos del alumno: ni nombre, ni email, ni AlumnoId (privacidad).
public sealed record SolicitudCorreccionIa(
    Guid InstitucionId,
    Guid EvaluacionId,
    Guid EntregaId,
    Guid PreguntaId,
    string Enunciado,
    string Respuesta,
    string RubricaJson,
    decimal PuntajeMaximo);

public interface ICorreccionIaClient
{
    // true si IA respondió OK. La nota NO se toma de acá: llega por el callback de la tool MCP
    // a /interno/entregas/{entregaId}/calificacion-sugerida (decisión del PO, RN-COR-06).
    Task<bool> SolicitarCorreccionAsync(SolicitudCorreccionIa solicitud, CancellationToken ct);
}
