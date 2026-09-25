using Orders.Authentication;
using Stripe;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Orders.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        // Creates an Order from the caller's current basket and starts a Stripe Checkout
        // Session for it. Returns the URL the browser should be redirected to.
        group.MapPost("/checkout", async (
            ClaimsPrincipal user,
            BasketApiClient basketApiClient,
            OrderService orderService,
            StripePaymentService stripePaymentService) =>
        {
            var userName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? user.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Results.Unauthorized();
            }

            var cart = await basketApiClient.GetBasket(userName);
            if (cart is null || cart.Items.Count == 0)
            {
                return Results.BadRequest("Your basket is empty.");
            }

            var order = orderService.CreateFromCart(cart);
            await orderService.SaveChangesAsync();

            Stripe.Checkout.Session session;
            try
            {
                session = await stripePaymentService.CreateCheckoutSessionAsync(order);
            }
            catch (StripeException ex)
            {
                return Results.Problem($"Failed to start checkout with Stripe: {ex.Message}");
            }

            order.StripeSessionId = session.Id;
            await orderService.SaveChangesAsync();

            return Results.Ok(new { orderId = order.Id, checkoutUrl = session.Url });
        })
        .WithName("Checkout")
        .RequireAuthorization("UserOnly");

        // Called by Stripe (not the browser) when a Checkout Session's payment completes.
        group.MapPost("/webhook", async (
            HttpRequest request,
            OrderService orderService,
            BasketApiClient basketApiClient,
            StripePaymentService stripePaymentService,
            InternalTokenService internalTokenService) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();

            Event stripeEvent;
            try
            {
                stripeEvent = stripePaymentService.ConstructWebhookEvent(json, request.Headers["Stripe-Signature"]!);
            }
            catch (StripeException)
            {
                return Results.BadRequest("Invalid Stripe signature.");
            }

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted &&
                stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                var order = await orderService.GetByStripeSessionIdAsync(session.Id);
                if (order is not null && order.Status != OrderStatus.Paid)
                {
                    order.Status = OrderStatus.Paid;
                    order.PaidAtUtc = DateTime.UtcNow;
                    await orderService.SaveChangesAsync();

                    // No logged-in user is attached to this webhook call, so we use an
                    // internally minted service token to clear the customer's basket.
                    var serviceToken = internalTokenService.CreateServiceToken();
                    await basketApiClient.DeleteBasket(order.UserName, serviceToken);
                }
            }

            return Results.Ok();
        })
        .WithName("StripeWebhook")
        .AllowAnonymous();

        // GET order details, used by the order-confirmation page.
        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, OrderService orderService) =>
        {
            var order = await orderService.GetByIdAsync(id);
            if (order is null)
            {
                return Results.NotFound();
            }

            if (!user.OwnsOrder(order))
            {
                return Results.Forbid();
            }

            return Results.Ok(order);
        })
        .WithName("GetOrder")
        .RequireAuthorization("UserOnly");

        // Generates and downloads a PDF invoice — only once the order has actually been paid.
        group.MapGet("/{id:int}/invoice", async (int id, ClaimsPrincipal user, OrderService orderService, Orders.Services.InvoiceService invoiceService) =>
        {
            var order = await orderService.GetByIdAsync(id);
            if (order is null)
            {
                return Results.NotFound();
            }

            if (!user.OwnsOrder(order))
            {
                return Results.Forbid();
            }

            if (order.Status != OrderStatus.Paid)
            {
                return Results.BadRequest("This order has not been paid yet.");
            }

            var pdfBytes = invoiceService.GenerateInvoicePdf(order);
            return Results.File(pdfBytes, "application/pdf", $"invoice-{order.Id}.pdf");
        })
        .WithName("GetOrderInvoice")
        .RequireAuthorization("UserOnly");
    }

    private static bool OwnsOrder(this ClaimsPrincipal user, Order order)
    {
        if (user.IsInRole("Admin"))
        {
            return true;
        }

        var owner = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? user.Identity?.Name;
        return string.Equals(owner, order.UserName, StringComparison.OrdinalIgnoreCase);
    }
}
