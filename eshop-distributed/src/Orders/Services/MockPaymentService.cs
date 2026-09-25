namespace Orders.Services;

// Fake payment provider for local development: no keys, no webhook, no Stripe CLI.
// The customer is sent to the React app's /mock-payment page, whose "Pay" button calls
// POST /orders/{id}/mock-pay to mark the order paid.
public class MockPaymentService(IConfiguration configuration) : IPaymentService
{
    private readonly string _webAppBaseUrl = configuration["Payment:WebAppBaseUrl"]
        ?? throw new InvalidOperationException("Payment:WebAppBaseUrl is not configured.");

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Order order) =>
        Task.FromResult(new CheckoutSession(null, $"{_webAppBaseUrl}/mock-payment?orderId={order.Id}"));
}
