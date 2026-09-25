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

    public async Task<bool> RemoveFromBasketAsync(string userName, string? accessToken, int productId)
    {
        var cart = await basketApiClient.GetBasket(userName, accessToken);
        if (cart is null)
        {
            return false;
        }

        cart.Items.RemoveAll(i => i.ProductId == productId);

        return await basketApiClient.UpdateBasket(cart, accessToken);
    }

    // delta is +1 or -1. Quantity is never allowed to drop below 1 here — use
    // RemoveFromBasketAsync to take an item out of the basket entirely.
    public async Task<bool> ChangeQuantityAsync(string userName, string? accessToken, int productId, int delta)
    {
        var cart = await basketApiClient.GetBasket(userName, accessToken);
        var item = cart?.Items.FirstOrDefault(i => i.ProductId == productId);
        if (cart is null || item is null)
        {
            return false;
        }

        item.Quantity = Math.Max(1, item.Quantity + delta);

        return await basketApiClient.UpdateBasket(cart, accessToken);
    }
}
