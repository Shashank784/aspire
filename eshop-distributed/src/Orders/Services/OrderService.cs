using Azure.Messaging.ServiceBus;
using ServiceDefaults.Messaging.Events;

namespace Orders.Services;

public class OrderService(OrderDbContext dbContext, ServiceBusClient serviceBusClient)
{
    public Order CreateFromCart(ShoppingCart cart)
    {
        var order = new Order
        {
            UserName = cart.UserName,
            Items = cart.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Price = item.Price,
                Quantity = item.Quantity
            }).ToList(),
            TotalAmount = cart.TotalPrice
        };

        dbContext.Orders.Add(order);
        return order;
    }

    public Task<Order?> GetByIdAsync(int id) =>
        dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

    public Task<Order?> GetByStripeSessionIdAsync(string sessionId) =>
        dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.StripeSessionId == sessionId);

    // Shared by the Stripe webhook and the mock "Pay" button. Safe to call twice.
    public async Task MarkPaidAsync(Order order)
    {
        if (order.Status == OrderStatus.Paid)
        {
            return;
        }

        order.Status = OrderStatus.Paid;
        order.PaidAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Basket (clears the basket) and Notification (sends the email) each have their own
        // subscription on the "order-events" topic — Orders doesn't know or call either of them.
        // Note: saving and publishing aren't atomic; the Outbox pattern would close that gap.
        var integrationEvent = new OrderPaidIntegrationEvent
        {
            OrderId = order.Id,
            UserName = order.UserName,
            TotalAmount = order.TotalAmount,
            PaidAtUtc = order.PaidAtUtc.Value,
            Items = order.Items.Select(i => new OrderPaidItem(i.ProductName, i.Price, i.Quantity)).ToList()
        };

        await using var sender = serviceBusClient.CreateSender("order-events");
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(integrationEvent))
        {
            Subject = nameof(OrderPaidIntegrationEvent),
            MessageId = $"order-paid-{order.Id}"
        });
    }

    public async Task SaveChangesAsync() => await dbContext.SaveChangesAsync();
}
