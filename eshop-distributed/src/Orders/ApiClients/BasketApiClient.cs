using System.Net.Http.Json;

namespace Orders.ApiClients;

// The caller's own JWT is attached by ForwardedBearerHandler, so Basket's
// ownership check sees the same user who called Orders.
public class BasketApiClient(HttpClient httpClient)
{
    public async Task<ShoppingCart?> GetBasket(string userName)
    {
        var response = await httpClient.GetAsync($"/basket/{userName}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShoppingCart>()
            : null;
    }
}
