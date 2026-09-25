namespace ServiceDefaults.Messaging.Events;

public record OrderPaidIntegrationEvent
{
    public int OrderId { get; init; }
    public string UserName { get; init; } = default!;   // this is also the customer's email
    public decimal TotalAmount { get; init; }
    public DateTime PaidAtUtc { get; init; }
    public List<OrderPaidItem> Items { get; init; } = [];
}

public record OrderPaidItem(string ProductName, decimal Price, int Quantity);
