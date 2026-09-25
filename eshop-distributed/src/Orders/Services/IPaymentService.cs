namespace Orders.Services;

// SessionId is the provider's reference for the payment (Stripe Checkout Session id);
// CheckoutUrl is where the browser goes to pay.
public record CheckoutSession(string? SessionId, string CheckoutUrl);

// Chosen by the "Payment:Provider" setting: "Mock" (default) or "Stripe".
public interface IPaymentService
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(Order order);
}
