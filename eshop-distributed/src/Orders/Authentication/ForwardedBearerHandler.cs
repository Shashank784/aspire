namespace Orders.Authentication;

// Forwards the caller's own "Authorization: Bearer ..." header onto outgoing
// calls to Basket, so Basket's ownership check (OwnsBasketFor) sees the same user.
public class ForwardedBearerHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrEmpty(authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
