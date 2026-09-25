using Azure.Messaging.ServiceBus;
using SendGrid;
using SendGrid.Helpers.Mail;
using ServiceDefaults.Messaging.Events;
using System.Globalization;
using System.Net;
using System.Text;

namespace Notification;

// Receives OrderPaid events from the "notification" subscription of the "order-events"
// topic and emails the customer an order confirmation through SendGrid.
public class OrderPaidEmailListener(
    ServiceBusClient client,
    ISendGridClient sendGrid,
    IConfiguration configuration,
    ILogger<OrderPaidEmailListener> logger) : BackgroundService
{
    private static readonly CultureInfo Inr = CultureInfo.GetCultureInfo("en-IN");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor("order-events", "notification");

        processor.ProcessMessageAsync += async args =>
        {
            var orderPaid = args.Message.Body.ToObjectFromJson<OrderPaidIntegrationEvent>()!;
            var response = await sendGrid.SendEmailAsync(BuildEmail(orderPaid), args.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Sent order confirmation for order {OrderId} to {Email}", orderPaid.OrderId, orderPaid.UserName);
                return;
            }

            var error = await response.Body.ReadAsStringAsync();

            // 4xx (bad address, unverified sender, wrong key...) won't fix itself on retry, so park
            // the message in the dead-letter queue. Anything else (5xx, 429) throws so Service Bus retries.
            if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                logger.LogError("SendGrid rejected email for order {OrderId}: {StatusCode} {Error}", orderPaid.OrderId, response.StatusCode, error);
                await args.DeadLetterMessageAsync(args.Message, "SendGridRejected", $"{response.StatusCode}: {error}");
                return;
            }

            throw new InvalidOperationException($"SendGrid failed with {response.StatusCode}: {error}");
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

    private SendGridMessage BuildEmail(OrderPaidIntegrationEvent orderPaid)
    {
        var from = new EmailAddress(configuration["SendGrid:FromEmail"], "eShop");
        var to = new EmailAddress(orderPaid.UserName);
        var subject = $"Your eShop order #{orderPaid.OrderId} is confirmed";

        var rows = new StringBuilder();
        foreach (var item in orderPaid.Items)
        {
            rows.Append($"""
                <tr>
                  <td style="padding:6px 8px;border-bottom:1px solid #eee">{WebUtility.HtmlEncode(item.ProductName)}</td>
                  <td style="padding:6px 8px;border-bottom:1px solid #eee;text-align:center">{item.Quantity}</td>
                  <td style="padding:6px 8px;border-bottom:1px solid #eee;text-align:right">{(item.Price * item.Quantity).ToString("C", Inr)}</td>
                </tr>
                """);
        }

        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;color:#16181d">
              <h2 style="color:#2f6f4f">Thank you for your order!</h2>
              <p>Your payment for order <strong>#{orderPaid.OrderId}</strong> was received on
                 {orderPaid.PaidAtUtc:dd MMM yyyy, HH:mm} UTC.</p>
              <table style="width:100%;border-collapse:collapse;font-size:14px">
                <thead>
                  <tr style="text-align:left;color:#5d6573">
                    <th style="padding:6px 8px">Product</th>
                    <th style="padding:6px 8px;text-align:center">Qty</th>
                    <th style="padding:6px 8px;text-align:right">Subtotal</th>
                  </tr>
                </thead>
                <tbody>{rows}</tbody>
              </table>
              <p style="text-align:right;font-size:16px"><strong>Total: {orderPaid.TotalAmount.ToString("C", Inr)}</strong></p>
              <p style="color:#5d6573;font-size:13px">You can download your invoice from the order confirmation page.</p>
            </div>
            """;

        var plainText = $"Thank you for your order! Order #{orderPaid.OrderId} — total {orderPaid.TotalAmount.ToString("C", Inr)}.";

        return MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
    }
}
