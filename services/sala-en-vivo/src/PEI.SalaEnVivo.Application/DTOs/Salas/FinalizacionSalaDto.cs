namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    public sealed record FinalizacionSalaDto(
        Guid SalaId,
        DateTime FinalizadaUtc,
        string Motivo,
        bool YaEstabaFinalizada);
}
