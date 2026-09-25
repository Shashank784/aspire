using Orders.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WebApp.ApiClients;

public record CheckoutResult(int OrderId, string CheckoutUrl);

public class OrdersApiClient(HttpClient httpClient)
{
    // accessToken is optional for the same reason as BasketApiClient/CatalogApiClient:
    // it must be passed explicitly from interactive Blazor Server event handlers, where
    // the ambient HttpContext (which BearerTokenHandler otherwise relies on) isn't available.
    public async Task<CheckoutResult?> Checkout(string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders/checkout");
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CheckoutResult>()
            : null;
    }

    public async Task<Order?> GetOrder(int id, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/orders/{id}");
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Order>()
            : null;
    }

    public async Task<byte[]?> DownloadInvoicePdf(int id, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/orders/{id}/invoice");
        ApplyToken(request, accessToken);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
    }

    private static void ApplyToken(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}
