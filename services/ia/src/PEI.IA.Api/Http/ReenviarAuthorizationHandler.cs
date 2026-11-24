namespace PEI.IA.Api.Http;

/// <summary>
/// S3-IA-02: reenvía a Cursos el token del docente que hizo el pedido.
/// El interno GET /api/v1/materiales/{id}/texto tiene [Authorize]: con el token valida que no sea alumno
/// y que el apunte sea de su institución (sin token responde 401 y no se llega al modelo).
/// </summary>
public sealed class ReenviarAuthorizationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReenviarAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && request.Headers.Authorization is null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
