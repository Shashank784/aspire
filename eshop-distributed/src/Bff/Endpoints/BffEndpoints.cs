using Bff.Authentication;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Bff.Endpoints;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FullName);
public record UserInfo(bool IsAuthenticated, string? Name, IEnumerable<string> Roles);

public static class BffEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/user", (ClaimsPrincipal user) => Results.Ok(ToUserInfo(user)));

        group.MapPost("/login", async (LoginRequest request, HttpContext httpContext, IdentityApiClient identityApiClient) =>
        {
            var result = await identityApiClient.LoginAsync(request.Email, request.Password);
            if (result.Response is null)
            {
                return Results.Problem(result.Error, statusCode: StatusCodes.Status401Unauthorized);
            }

            var principal = await AuthSession.SignInAsync(httpContext, request.Email, result.Response);
            return Results.Ok(ToUserInfo(principal));
        });

        // New accounts always get the "User" role — becoming an Admin happens out of band.
        group.MapPost("/register", async (RegisterRequest request, HttpContext httpContext, IdentityApiClient identityApiClient) =>
        {
            var result = await identityApiClient.RegisterAsync(request.Email, request.Password, request.FullName);
            if (result.Response is null)
            {
                return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
            }

            var principal = await AuthSession.SignInAsync(httpContext, request.Email, result.Response);
            return Results.Ok(ToUserInfo(principal));
        });

        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await AuthSession.SignOutAsync(httpContext);
            return Results.NoContent();
        });
    }

    // /api/products/** -> Catalog, /api/basket/** -> Basket, /api/orders/** -> Orders.
    // The cookie is stripped and replaced by the user's JWT, so the services keep
    // doing their own JWT validation and ownership checks exactly as before.
    public static void MapApiForwarders(this IEndpointRouteBuilder app)
    {
        app.MapForwarder("/api/products/{**catch-all}", "https+http://catalog", ForwardWithBearerToken);
        app.MapForwarder("/api/basket/{**catch-all}", "https+http://basket", ForwardWithBearerToken);
        app.MapForwarder("/api/orders/{**catch-all}", "https+http://orders", ForwardWithBearerToken);
    }

    private static void ForwardWithBearerToken(TransformBuilderContext context)
    {
        context.AddPathRemovePrefix("/api");
        context.AddRequestHeaderRemove("Cookie");
        context.AddRequestTransform(transform =>
        {
            var accessToken = transform.HttpContext.User.FindFirstValue(AuthClaimTypes.AccessToken);
            if (!string.IsNullOrEmpty(accessToken))
            {
                transform.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
            }

            return ValueTask.CompletedTask;
        });
    }

    private static UserInfo ToUserInfo(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? new UserInfo(true, user.Identity.Name, user.FindAll(ClaimTypes.Role).Select(c => c.Value))
            : new UserInfo(false, null, []);
}
