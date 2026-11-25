using System;
using System.Collections.Generic;

namespace PEI.SalaEnVivo.Application.DTOs.Entregas
{
    /// <summary>
    /// Cuerpo de la petición POST /interno/salas/{salaId}/entregas hacia el microservicio de Evaluaciones.
    /// Especificado en contracts/schemas/lote-entregas-v1.schema.json.
    /// </summary>
    public sealed record LoteEntregasRequestDto(
        int Version,
        Guid LoteId,
        Guid SalaId,
        Guid EvaluacionId,
        Guid InstitucionId,
        DateTime FinalizadaUtc,
        string MotivoCierre,
        List<EntregaAlumnoDto> Entregas);

    /// <summary>
    /// Representa la entrega individual de un alumno que ingresó a la sala.
    /// El AlumnoId es el claim sub del alumno; de un alumno con cuenta nunca viaja nombre ni email.
    /// S3-SA-01: del invitado (sin cuenta) viaja el nombre que escribió al entrar, para que el docente lo reconozca.
    /// </summary>
    public sealed record EntregaAlumnoDto(
        Guid AlumnoId,
        List<RespuestaEntregaDto> Respuestas,
        List<AlertaFocoEntregaDto> AlertasFoco,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string? NombreInvitado = null);

    /// <summary>
    /// Respuesta aceptada del alumno a una pregunta de la evaluación.
    /// </summary>
    public sealed record RespuestaEntregaDto(
        Guid PreguntaId,
        string Respuesta,
        int Secuencia,
        DateTime RecibidaUtc);

    /// <summary>
    /// Evento de telemetría de pérdida de foco registrado durante el examen.
    /// Indicador pedagógico para el docente que nunca modifica la nota automáticamente (RN-CMP-07).
    /// </summary>
    public sealed record AlertaFocoEntregaDto(
        Guid? PreguntaId,
        string TipoEvento,
        int DuracionMs,
        string Severidad,
        DateTime RegistradoUtc);

    /// <summary>
    /// Respuesta 200 de Evaluaciones al recibir el lote de entregas.
    /// Especificado en contracts/schemas/resultado-lote-entregas-v1.schema.json.
    /// </summary>
    public sealed record ResultadoLoteEntregasDto(
        Guid LoteId,
        Guid SalaId,
        int EntregasRecibidas,
        int EntregasNuevas,
        int EntregasYaExistentes,
        List<EntregaResultadoDto>? Entregas);

    /// <summary>
    /// Detalle de procesamiento de cada entrega dentro del lote en Evaluaciones.
    /// </summary>
    public sealed record EntregaResultadoDto(
        Guid AlumnoId,
        Guid EntregaId,
        string Estado,
        decimal? NotaMultipleChoice);
}
