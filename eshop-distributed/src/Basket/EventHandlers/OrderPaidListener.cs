using Azure.Messaging.ServiceBus;
using ServiceDefaults.Messaging.Events;

namespace Basket.EventHandlers;

// Receives OrderPaid events from the "basket" subscription of the "order-events" topic
// and empties the customer's basket. If handling throws, Service Bus redelivers the
// message and, after repeated failures, moves it to the subscription's dead-letter queue.
public class OrderPaidListener(ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<OrderPaidListener> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor("order-events", "basket");

        processor.ProcessMessageAsync += async args =>
        {
            var orderPaid = args.Message.Body.ToObjectFromJson<OrderPaidIntegrationEvent>()!;

            // BasketService is scoped; this listener lives for the whole app, so use a scope per message.
            using var scope = scopeFactory.CreateScope();
            var basketService = scope.ServiceProvider.GetRequiredService<BasketService>();
            await basketService.DeleteBasket(orderPaid.UserName);

            logger.LogInformation("Cleared basket for {UserName} after order {OrderId} was paid", orderPaid.UserName, orderPaid.OrderId);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus error in {Source} for {EntityPath}", args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down.
        }

        await processor.StopProcessingAsync();
    }
}
