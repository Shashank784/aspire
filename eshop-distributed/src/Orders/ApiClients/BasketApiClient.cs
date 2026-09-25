using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Orders.ApiClients;

public class BasketApiClient(HttpClient httpClient)
{
    // bearerToken is optional: when set (e.g. the internal service token used from the
    // Stripe webhook, which has no forwarded user request to draw a token from) it overrides
    // whatever ForwardedBearerHandler would otherwise attach.
    public async Task<ShoppingCart?> GetBasket(string userName, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/basket/{userName}");
        ApplyToken(request, bearerToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShoppingCart>()
            : null;
    }

    public async Task<bool> DeleteBasket(string userName, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/basket/{userName}");
        ApplyToken(request, bearerToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private static void ApplyToken(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}
