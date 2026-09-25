using Orders.Authentication;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<OrderDbContext>(connectionName: "ordersdb");
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<InvoiceService>();
builder.AddAzureServiceBusClient("servicebus");


// "Mock" (default) needs no keys; "Stripe" needs Stripe:SecretKey and Stripe:WebhookSecret.
// StripePaymentService is always registered because the webhook endpoint uses it.
builder.Services.AddSingleton<StripePaymentService>();
var paymentProvider = builder.Configuration["Payment:Provider"] ?? "Mock";
if (paymentProvider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
{
    var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
    if (string.IsNullOrEmpty(stripeSecretKey))
    {
        throw new InvalidOperationException("Payment:Provider is Stripe but Stripe:SecretKey is not configured.");
    }

    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
    builder.Services.AddSingleton<IPaymentService>(sp => sp.GetRequiredService<StripePaymentService>());
}
else
{
    builder.Services.AddSingleton<IPaymentService, MockPaymentService>();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardedBearerHandler>();

builder.Services.AddHttpClient<BasketApiClient>(client =>
{
    client.BaseAddress = new("https+http://basket");
})
.AddHttpMessageHandler<ForwardedBearerHandler>();

builder.AddJwtValidation();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseMigration();

app.MapOrderEndpoints();

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpsRedirection();

app.Run();
