using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Orders.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        // Creates a Pending Order from the caller's current basket. The React app then opens
        // its /mock-payment page for that order.
        group.MapPost("/checkout", async (
            ClaimsPrincipal user,
            BasketApiClient basketApiClient,
            OrderService orderService) =>
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

            return Results.Ok(new { orderId = order.Id });
        })
        .WithName("Checkout")
        .RequireAuthorization("UserOnly");

        // The mock payment page's "Pay" button. There is no real payment provider in this
        // project, so paying just marks the order Paid (no real money moves).
        group.MapPost("/{id:int}/mock-pay", async (int id, ClaimsPrincipal user, OrderService orderService) =>
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

        // GET the caller's own orders (order history page).
        group.MapGet("/", async (ClaimsPrincipal user, OrderService orderService) =>
        {
            var userName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? user.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await orderService.GetByUserAsync(userName));
        })
        .WithName("GetMyOrders")
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
