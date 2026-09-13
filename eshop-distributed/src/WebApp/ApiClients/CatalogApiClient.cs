using Catalog.Models;

namespace WebApp.ApiClients;

public class CatalogApiClient(HttpClient httpClient)
{
    public async Task<List<Product>> GetProducts()
    {
        var response = await httpClient.GetFromJsonAsync<List<Product>>($"/products");
        return response!;
    }

    public async Task<Product> GetProductById(int id)
    {
        var response = await httpClient.GetFromJsonAsync<Product>($"/products/{id}");
        return response!;
    }

    public async Task<List<Product>?> SearchProducts(string query)
    {
        return await httpClient.GetFromJsonAsync<List<Product>>($"/products/search/{query}");
    }

    // Admin-only on the Catalog side (RequireAuthorization("AdminOnly")).
    public async Task<Product?> CreateProduct(Product product)
    {
        var response = await httpClient.PostAsJsonAsync("/products/", product);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<Product>();
    }
}
