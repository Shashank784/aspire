using System.Net.Http.Json;
using System.Text.Json;

namespace Bff.Authentication;

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, IEnumerable<string> Roles);
public record AuthResult(AuthResponse? Response, string? Error);

public class IdentityApiClient(HttpClient httpClient)
{
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var response = await httpClient.PostAsJsonAsync("/auth/login", new { Email = email, Password = password });

        return response.IsSuccessStatusCode
            ? new AuthResult(await response.Content.ReadFromJsonAsync<AuthResponse>(), null)
            : new AuthResult(null, "Invalid email or password.");
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string? fullName)
    {
        var response = await httpClient.PostAsJsonAsync("/auth/register", new { Email = email, Password = password, FullName = fullName });

        return response.IsSuccessStatusCode
            ? new AuthResult(await response.Content.ReadFromJsonAsync<AuthResponse>(), null)
            : new AuthResult(null, await ReadErrorAsync(response, "Registration failed."));
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var response = await httpClient.PostAsJsonAsync("/auth/refresh", new { RefreshToken = refreshToken });

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthResponse>()
            : null;
    }

    // Identity returns either a plain string (e.g. 409 "already exists") or a
    // ValidationProblem ({ errors: { code: [description] } }) — flatten both to one message.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            using var json = JsonDocument.Parse(body);

            if (json.RootElement.ValueKind == JsonValueKind.String)
            {
                return json.RootElement.GetString() ?? fallback;
            }

            if (json.RootElement.TryGetProperty("errors", out var errors))
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString()))
                    .Where(m => !string.IsNullOrEmpty(m));

                return string.Join(" ", messages);
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }
}
