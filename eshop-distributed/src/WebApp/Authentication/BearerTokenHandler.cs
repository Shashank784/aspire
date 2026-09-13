namespace WebApp.Authentication;

// Attaches the JWT access token (issued by the Identity service and stashed in the
// WebApp's own auth cookie) as a Bearer header on outgoing calls to Basket/Catalog.
public class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = httpContextAccessor.HttpContext?.User
            .FindFirst(AuthClaimTypes.AccessToken)?.Value;

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
