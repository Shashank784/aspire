using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace Basket.Services;

public class BasketService(IDistributedCache cache, IConnectionMultiplexer redis, CatalogApiClient catalogApiClient)
{
    // IDistributedCache can't list its keys, so keep a Redis set of every user who has a basket.
    private const string BasketUsersKey = "basket:users";

    public async Task<ShoppingCart?> GetBasket(string userName)
    {
        var basket = await cache.GetStringAsync(userName);
        return string.IsNullOrEmpty(basket) ? null :
            JsonSerializer.Deserialize<ShoppingCart>(basket);
    }
    public async Task UpdateBasket(ShoppingCart basket)
    {
        // Before update(Add/remove Item) into SC, we should call Catalog ms GetProductById method
        // Get latest product information and set Price and ProductName when adding item into SC
        foreach (var item in basket.Items)
        {
            var product = await catalogApiClient.GetProductById(item.ProductId);
            item.Price = product.Price;
            item.ProductName = product.Name;
        }

        await cache.SetStringAsync(basket.UserName, JsonSerializer.Serialize(basket));
        await redis.GetDatabase().SetAddAsync(BasketUsersKey, basket.UserName);
    }
    public async Task DeleteBasket(string userName)
    {
        await cache.RemoveAsync(userName);
        await redis.GetDatabase().SetRemoveAsync(BasketUsersKey, userName);
    }

    // Called when a product's price changes: update that product in every basket that holds it.
    internal async Task UpdateBasketItemProductPrices(int productId, decimal price)
    {
        var db = redis.GetDatabase();

        foreach (var member in await db.SetMembersAsync(BasketUsersKey))
        {
            var userName = member.ToString();
            var basket = await GetBasket(userName);
            if (basket is null)
            {
                await db.SetRemoveAsync(BasketUsersKey, userName); // basket is gone, stop tracking it
                continue;
            }

            var items = basket.Items.Where(x => x.ProductId == productId).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            foreach (var item in items)
            {
                item.Price = price;
            }
            await cache.SetStringAsync(basket.UserName, JsonSerializer.Serialize(basket));
        }
    }
}
