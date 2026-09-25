namespace Orders.Services;

public class OrderService(OrderDbContext dbContext)
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

    public async Task SaveChangesAsync() => await dbContext.SaveChangesAsync();
}
