using Stripe;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Orders.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        // Creates an Order from the caller's current basket and starts a checkout with the
        // configured payment provider. Returns the URL the browser should be redirected to.
        group.MapPost("/checkout", async (
            ClaimsPrincipal user,
            BasketApiClient basketApiClient,
            OrderService orderService,
            IPaymentService paymentService) =>
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

            CheckoutSession session;
            try
            {
                session = await paymentService.CreateCheckoutSessionAsync(order);
            }
            catch (StripeException ex)
            {
                return Results.Problem($"Failed to start checkout with Stripe: {ex.Message}");
            }

            order.StripeSessionId = session.SessionId;
            await orderService.SaveChangesAsync();

            return Results.Ok(new { orderId = order.Id, checkoutUrl = session.CheckoutUrl });
        })
        .WithName("Checkout")
        .RequireAuthorization("UserOnly");

        // Called by Stripe (not the browser) when a Checkout Session's payment completes.
        group.MapPost("/webhook", async (
            HttpRequest request,
            OrderService orderService,
            StripePaymentService stripePaymentService) =>
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
                if (order is not null)
                {
                    await orderService.MarkPaidAsync(order);
                }
            }

            return Results.Ok();
        })
        .WithName("StripeWebhook")
        .AllowAnonymous();

        // The mock provider's "Pay" button. Only exists when Payment:Provider is Mock —
        // otherwise anyone could mark their own order paid without paying.
        group.MapPost("/{id:int}/mock-pay", async (int id, ClaimsPrincipal user, OrderService orderService, IPaymentService paymentService) =>
        {
            if (paymentService is not MockPaymentService)
            {
                return Results.NotFound();
            }

            var order = await orderService.GetByIdAsync(id);
            if (order is null)
            {
                return Results.NotFound();
            }

            if (!user.OwnsOrder(order))
            {
                return Results.Forbid();
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return Results.BadRequest("This order was cancelled.");
            }

            await orderService.MarkPaidAsync(order);
            return Results.Ok(order);
        })
        .WithName("MockPayOrder")
        .RequireAuthorization("UserOnly");

        // Called when the customer backs out of payment. Only unpaid orders can be cancelled.
        group.MapPost("/{id:int}/cancel", async (int id, ClaimsPrincipal user, OrderService orderService) =>
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

            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Cancelled;
                await orderService.SaveChangesAsync();
            }

            return Results.Ok(order);
        })
        .WithName("CancelOrder")
        .RequireAuthorization("UserOnly");

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
