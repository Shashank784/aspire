using Basket.Models;
using Catalog.Models;
using WebApp.ApiClients;

namespace WebApp;

// Shared "add to basket" logic used by both the Products and Search pages.
public class BasketActions(BasketApiClient basketApiClient)
{
    public async Task<bool> AddToBasketAsync(string userName, string? accessToken, Product product)
    {
        var cart = await basketApiClient.GetBasket(userName, accessToken) ?? new ShoppingCart { UserName = userName };

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem is not null)
        {
            existingItem.Quantity++;
        }
        else
        {
            cart.Items.Add(new ShoppingCartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = 1,
                Color = "Default"
            });
        }

        return await basketApiClient.UpdateBasket(cart, accessToken);
    }
}
