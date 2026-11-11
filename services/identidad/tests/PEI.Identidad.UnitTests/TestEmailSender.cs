using PEI.Identidad.Application.Plataforma;

namespace PEI.Identidad.UnitTests;

internal sealed class TestEmailSender : IEmailSender
{
    public Task EnviarAsync(MensajeEmail mensaje, CancellationToken cancellationToken) => Task.CompletedTask;
}
