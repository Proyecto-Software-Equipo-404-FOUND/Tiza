using PEI.Identidad.Application.Interfaces.Service;

namespace PEI.Identidad.Application.UseCases.Auth.Invitado;

// S3-SA-01 (#231): ingreso de un alumno invitado (sin cuenta) con PIN + nombre.
// Contrato: identidad-v1.json, POST /api/auth/invitado. Identidad consulta a Sala en vivo
// (GET /interno/salas/pin/{pin}) y, si la sala admite invitados, emite un JWT de rol Invitado
// válido solo para esa sala (claim sala_id). Ningún servicio fuera de Sala acepta ese rol.

public sealed record IngresoInvitadoCommand(int Pin, string? Nombre);

public sealed record SalaParaInvitado(Guid SalaId, Guid InstitucionId, bool PermiteInvitados, string Estado);

/// <summary>Consulta a Sala en vivo por el endpoint interno. Devuelve null si no hay sala abierta con ese PIN.</summary>
public interface ISalaInvitadosClient
{
    Task<SalaParaInvitado?> ObtenerPorPinAsync(int pin, CancellationToken ct);
}

public interface IInvitadoTokenIssuer
{
    IssuedAccessToken IssueInvitado(Guid invitadoId, Guid institucionId, Guid salaId, string nombre);
}

public enum ResultadoIngresoInvitado
{
    Ok,
    NombreInvalido,
    SalaNoEncontrada,
    SalaSinInvitados,
    SalaFinalizada,
    SalaNoDisponible
}

public sealed record SesionInvitado(string AccessToken, DateTime ExpiraEnUtc, Guid SalaId, string Nombre);

public sealed record IngresoInvitadoRespuesta(ResultadoIngresoInvitado Resultado, SesionInvitado? Sesion = null);

public sealed class IngresoInvitadoHandler(ISalaInvitadosClient salas, IInvitadoTokenIssuer tokens)
{
    public const int NombreMinimo = 2;
    public const int NombreMaximo = 60;

    public async Task<IngresoInvitadoRespuesta> HandleAsync(IngresoInvitadoCommand command, CancellationToken ct)
    {
        var nombre = (command.Nombre ?? string.Empty).Trim();
        if (nombre.Length < NombreMinimo || nombre.Length > NombreMaximo)
            return new(ResultadoIngresoInvitado.NombreInvalido);

        if (command.Pin is < 100000 or > 999999)
            return new(ResultadoIngresoInvitado.SalaNoEncontrada);

        SalaParaInvitado? sala;
        try
        {
            sala = await salas.ObtenerPorPinAsync(command.Pin, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new(ResultadoIngresoInvitado.SalaNoDisponible);
        }

        if (sala is null) return new(ResultadoIngresoInvitado.SalaNoEncontrada);
        if (string.Equals(sala.Estado, "Finalizada", StringComparison.OrdinalIgnoreCase))
            return new(ResultadoIngresoInvitado.SalaFinalizada);
        if (!sala.PermiteInvitados) return new(ResultadoIngresoInvitado.SalaSinInvitados);

        var emitido = tokens.IssueInvitado(Guid.NewGuid(), sala.InstitucionId, sala.SalaId, nombre);
        return new(ResultadoIngresoInvitado.Ok, new SesionInvitado(emitido.AccessToken, emitido.ExpiresAt, sala.SalaId, nombre));
    }
}
