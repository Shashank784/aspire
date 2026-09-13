using Basket.Models;

namespace WebApp.ApiClients;

public class BasketApiClient(HttpClient httpClient)
{
    public async Task<ShoppingCart?> GetBasket(string userName)
    {
        var response = await httpClient.GetAsync($"/basket/{userName}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ShoppingCart>();
    }

    public async Task<bool> UpdateBasket(ShoppingCart cart)
    {
        var response = await httpClient.PostAsJsonAsync("/basket/", cart);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteBasket(string userName)
    {
        var response = await httpClient.DeleteAsync($"/basket/{userName}");
        return response.IsSuccessStatusCode;
    }
}
