using Stripe;
using Stripe.Checkout;

namespace Orders.Services;

public class StripePaymentService(IConfiguration configuration) : IPaymentService
{
    private readonly string _webAppBaseUrl = configuration["Payment:WebAppBaseUrl"]
        ?? throw new InvalidOperationException("Payment:WebAppBaseUrl is not configured.");

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(Order order)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            ClientReferenceId = order.Id.ToString(),
            LineItems = order.Items.Select(item => new SessionLineItemOptions
            {
                Quantity = item.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "inr",
                    UnitAmount = (long)Math.Round(item.Price * 100, MidpointRounding.AwayFromZero),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.ProductName
                    }
                }
            }).ToList(),
            SuccessUrl = $"{_webAppBaseUrl}/order-confirmation?orderId={order.Id}",
            CancelUrl = $"{_webAppBaseUrl}/order-cancelled?orderId={order.Id}"
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return new CheckoutSession(session.Id, session.Url);
    }

    // Verifies the request really came from Stripe (signed with our webhook secret)
    // before we trust anything in the payload.
    public Event ConstructWebhookEvent(string json, string stripeSignatureHeader)
    {
        var webhookSecret = configuration["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");

        return EventUtility.ConstructEvent(json, stripeSignatureHeader, webhookSecret);
    }
}
