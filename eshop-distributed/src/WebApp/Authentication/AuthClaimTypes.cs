namespace WebApp.Authentication;

// Custom claim types used to carry the Identity-service tokens inside the WebApp's own auth cookie.
public static class AuthClaimTypes
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
}
