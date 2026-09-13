using System.Net.Http.Json;

namespace WebApp.ApiClients;

public record AuthResult(bool Succeeded, AuthResponse? Response, string? Error);
public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, IEnumerable<string> Roles);

public class IdentityApiClient(HttpClient httpClient)
{
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var response = await httpClient.PostAsJsonAsync("/auth/login", new { Email = email, Password = password });

        if (!response.IsSuccessStatusCode)
        {
            return new AuthResult(false, null, "Invalid email or password.");
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new AuthResult(true, auth, null);
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string? fullName)
    {
        var response = await httpClient.PostAsJsonAsync("/auth/register", new { Email = email, Password = password, FullName = fullName });

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync();
            return new AuthResult(false, null, string.IsNullOrWhiteSpace(problem) ? "Registration failed." : problem);
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new AuthResult(true, auth, null);
    }
}
