using PEI.Identidad.Application.UseCases.Auth.Login;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public sealed class RefreshHandler(InstitutionalSessionService sessions)
{
    public Task<LoginResult> HandleAsync(RefreshCommand command, CancellationToken ct = default) => sessions.RefreshAsync(command, ct);
}
