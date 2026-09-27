using Azure.Messaging.ServiceBus;
using ServiceDefaults.Messaging.Events;

namespace Basket.EventHandlers;

// Receives ProductPriceChanged events from the "basket" subscription of the "product-events"
// topic and updates the price of that product in every basket that holds it. If handling
// throws, Service Bus redelivers the message and, after repeated failures, dead-letters it.
public class ProductPriceChangedListener(ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<ProductPriceChangedListener> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor("product-events", "basket");

        processor.ProcessMessageAsync += async args =>
        {
            var priceChanged = args.Message.Body.ToObjectFromJson<ProductPriceChangedIntegrationEvent>()!;

            // BasketService is scoped; this listener lives for the whole app, so use a scope per message.
            using var scope = scopeFactory.CreateScope();
            var basketService = scope.ServiceProvider.GetRequiredService<BasketService>();
            await basketService.UpdateBasketItemProductPrices(priceChanged.ProductId, priceChanged.Price);

            logger.LogInformation("Updated basket prices for product {ProductId} to {Price}", priceChanged.ProductId, priceChanged.Price);
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
