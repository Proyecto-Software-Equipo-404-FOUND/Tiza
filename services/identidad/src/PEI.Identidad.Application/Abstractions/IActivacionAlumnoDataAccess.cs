namespace PEI.Identidad.Application.Abstractions;

public interface IActivacionAlumnoDataAccess
{
    Task ActivarAsync(string token, string passwordHash, CancellationToken ct);
}
