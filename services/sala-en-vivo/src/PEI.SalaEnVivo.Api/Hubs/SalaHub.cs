/*
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PEI.SalaEnVivo.Api.Hubs;

[Authorize]
public sealed class SalaHub : Hub
{
    private readonly ISender _sender;

    public SalaHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task JoinSala(Guid salaId)
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Usuario no identificado.");

        await _sender.Send(
            new JoinSalaCommand(salaId, userId));

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            salaId.ToString());
    }
}
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PEI.SalaEnVivo.Api.Hubs;

[Authorize]
public sealed class SalaHub : Hub
{
    public async Task Ping()
    {
        await Clients.Caller.SendAsync(
            "Pong",
            "SignalR connection works");
    }
}
