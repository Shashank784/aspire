using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Security.Claims;

namespace Bff.Authentication;

// The Identity service's JWT + refresh token live only inside the BFF's encrypted,
// HttpOnly auth cookie — the React app never sees them.
public static class AuthClaimTypes
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
    public const string ExpiresAtUtc = "expires_at_utc";
}

public class AuthSession(IdentityApiClient identityApiClient, IMemoryCache cache)
{
    // Refresh tokens are single-use (Identity rotates them), so when the browser fires several
    // API calls at once only one of them may refresh; the others reuse its result.
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public static async Task<ClaimsPrincipal> SignInAsync(HttpContext httpContext, string userName, AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, userName),
            new(AuthClaimTypes.AccessToken, auth.AccessToken),
            new(AuthClaimTypes.RefreshToken, auth.RefreshToken),
            new(AuthClaimTypes.ExpiresAtUtc, auth.ExpiresAtUtc.ToString("O"))
        };
        claims.AddRange(auth.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return principal;
    }

    // Swaps in a fresh access token when the current one is about to expire.
    // Returns false (and signs the user out) when the session can no longer be refreshed.
    public async Task<bool> EnsureFreshTokenAsync(HttpContext httpContext)
    {
        var user = httpContext.User;
        var expiresAt = DateTime.Parse(user.FindFirstValue(AuthClaimTypes.ExpiresAtUtc) ?? DateTime.MinValue.ToString("O"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (expiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            return true;
        }

        var refreshToken = user.FindFirstValue(AuthClaimTypes.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            await SignOutAsync(httpContext);
            return false;
        }

        AuthResponse? refreshed;
        await RefreshLock.WaitAsync();
        try
        {
            if (!cache.TryGetValue(refreshToken, out refreshed))
            {
                refreshed = await identityApiClient.RefreshAsync(refreshToken);
                if (refreshed is not null)
                {
                    cache.Set(refreshToken, refreshed, TimeSpan.FromMinutes(1));
                }
            }
        }
        finally
        {
            RefreshLock.Release();
        }

        if (refreshed is null)
        {
            await SignOutAsync(httpContext);
            return false;
        }

        httpContext.User = await SignInAsync(httpContext, user.Identity!.Name!, refreshed);
        return true;
    }

    public static async Task SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }
}
