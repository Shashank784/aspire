using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using ServiceDefaults.Messaging.Events;

namespace Catalog.Services;

// Reads go through HybridCache: a small in-memory cache (L1) in front of Redis (L2), with the
// database only hit on a miss. Every write clears all product entries via the shared tag, so
// the next read reloads fresh data.
public class ProductService(ProductDbContext dbContext, IBus bus, HybridCache cache, ILogger<ProductService> logger)
{
    private const string ProductsTag = "products";
    private static readonly string[] Tags = [ProductsTag];

    // Search results depend on free text, so there can be many of them — keep them briefly.
    private static readonly HybridCacheEntryOptions SearchEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        return await cache.GetOrCreateAsync(
            "catalog:products:all",
            async cancellationToken =>
            {
                logger.LogInformation("Cache miss: loading all products from the database");
                return await dbContext.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);
            },
            tags: Tags);
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await cache.GetOrCreateAsync(
            $"catalog:products:{id}",
            async cancellationToken =>
            {
                logger.LogInformation("Cache miss: loading product {ProductId} from the database", id);
                return await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            },
            tags: Tags);
    }

    // Writes must work on the tracked database row, never on a cached copy.
    public async Task<Product?> FindProductForUpdateAsync(int id)
    {
        return await dbContext.Products.FindAsync(id);
    }

    public async Task CreateProductAsync(Product product)
    {
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        await cache.RemoveByTagAsync(ProductsTag);
    }

    public async Task UpdateProductAsync(Product updatedProduct, Product inputProduct)
    {
        // if price has changed, raise ProductPriceChanged integration event
        if (updatedProduct.Price != inputProduct.Price)
        {
            // Publish product price changed integration event for update basket prices
            var integrationEvent = new ProductPriceChangedIntegrationEvent
            {
                ProductId = updatedProduct.Id, // Id only comes from db entity
                Name = inputProduct.Name,
                Description = inputProduct.Description,
                Price = inputProduct.Price, //set updated product price
                ImageUrl = inputProduct.ImageUrl
            };
            await bus.Publish(integrationEvent);
        }

        // update product with new values
        updatedProduct.Name = inputProduct.Name;
        updatedProduct.Description = inputProduct.Description;
        updatedProduct.ImageUrl = inputProduct.ImageUrl;
        updatedProduct.Price = inputProduct.Price;

        dbContext.Products.Update(updatedProduct);
        await dbContext.SaveChangesAsync();
        await cache.RemoveByTagAsync(ProductsTag);
    }

    public async Task DeleteProductAsync(Product deletedProduct)
    {
        dbContext.Products.Remove(deletedProduct);
        await dbContext.SaveChangesAsync();
        await cache.RemoveByTagAsync(ProductsTag);
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        return await cache.GetOrCreateAsync(
            $"catalog:search:{query}",
            async cancellationToken =>
            {
                logger.LogInformation("Cache miss: searching the database for {Query}", query);
                return await dbContext.Products
                    .AsNoTracking()
                    .Where(p => p.Name.Contains(query))
                    .ToListAsync(cancellationToken);
            },
            SearchEntryOptions,
            tags: Tags);
    }
}
