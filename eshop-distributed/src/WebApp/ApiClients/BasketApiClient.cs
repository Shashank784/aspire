using Basket.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WebApp.ApiClients;

public class BasketApiClient(HttpClient httpClient)
{
    // accessToken is optional: BearerTokenHandler covers static (non-interactive) requests via
    // the ambient HttpContext, but that context isn't available during Blazor Server interactive
    // event handlers (e.g. an @onclick on an @rendermode InteractiveServer page), so callers from
    // those contexts must pass the token explicitly.
    public async Task<ShoppingCart?> GetBasket(string userName, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/basket/{userName}");
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ShoppingCart>();
    }

    public async Task<bool> UpdateBasket(ShoppingCart cart, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/basket/")
        {
            Content = JsonContent.Create(cart)
        };
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteBasket(string userName, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/basket/{userName}");
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private static void ApplyToken(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}
