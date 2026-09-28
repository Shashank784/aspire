using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Hybrid;
using ServiceDefaults.Messaging.Events;

namespace Catalog.Services;

// Reads go through HybridCache: a small in-memory cache (L1) in front of Redis (L2), with the
// database only hit on a miss. Every write clears all product entries via the shared tag, so
// the next read reloads fresh data.
public class ProductService(ProductDbContext dbContext, ServiceBusClient serviceBusClient, HybridCache cache, ProductImageStorage imageStorage, ILogger<ProductService> logger)
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
            await using var sender = serviceBusClient.CreateSender("product-events");
            await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(integrationEvent))
            {
                Subject = nameof(ProductPriceChangedIntegrationEvent),
                MessageId = integrationEvent.EventId.ToString()
            });
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
        await imageStorage.DeleteIfUploadedAsync(deletedProduct.ImageUrl);
    }

    // Uploads the new image, points the product at it, then removes the old uploaded image.
    public async Task SetProductImageAsync(Product product, Stream content, string contentType)
    {
        var oldImageUrl = product.ImageUrl;

        product.ImageUrl = await imageStorage.UploadAsync(content, contentType);
        await dbContext.SaveChangesAsync();
        await cache.RemoveByTagAsync(ProductsTag);

        await imageStorage.DeleteIfUploadedAsync(oldImageUrl);
    }

    // Case-insensitive: "tent", "Tent" and "TENT" all match. Contains() becomes a
    // case-sensitive LIKE in Postgres, so use ILIKE instead.
    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        var term = query.Trim();

        // Escape LIKE wildcards so "%" or "_" typed by the user are matched literally.
        var pattern = "%" + term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

        return await cache.GetOrCreateAsync(
            $"catalog:search:{term.ToLowerInvariant()}", // same cache entry whatever the case
            async cancellationToken =>
            {
                logger.LogInformation("Cache miss: searching the database for {Query}", term);
                return await dbContext.Products
                    .AsNoTracking()
                    .Where(p => EF.Functions.ILike(p.Name, pattern, @"\"))
                    .OrderBy(p => p.Id)
                    .ToListAsync(cancellationToken);
            },
            SearchEntryOptions,
            tags: Tags);
    }
}
