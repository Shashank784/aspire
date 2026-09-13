using Catalog.Models;

namespace WebApp;

public static class ProductImageResolver
{
    // Seeded products store a bare filename that lives in a demo CDN repo; products
    // created via the Admin page instead store a real, directly-usable image URL.
    public static string Resolve(Product product)
    {
        if (Uri.TryCreate(product.ImageUrl, UriKind.Absolute, out _))
        {
            return product.ImageUrl;
        }

        return $"https://raw.githubusercontent.com/MicrosoftDocs/mslearn-dotnet-cloudnative/main/dotnet-docker/Products/wwwroot/images/{product.ImageUrl}";
    }
}
