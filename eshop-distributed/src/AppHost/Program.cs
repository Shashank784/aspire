
var builder = DistributedApplication.CreateBuilder(args);

// Backing Services
var postgres = builder
        .AddPostgres("postgres")
        .WithPgAdmin()
        //.WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var ordersDb = postgres.AddDatabase("ordersdb");

var cache = builder
    .AddRedis("cache")
    .WithRedisInsight()
    //.WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// RabbitMQ (MassTransit) still carries the product-price-changed event.
var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    //.WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Azure Service Bus (local emulator) carries order events. The emulator can't create
// entities at runtime, so the topic and its subscriptions are declared here.
var serviceBus = builder
    .AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

var orderEvents = serviceBus.AddServiceBusTopic("order-events");
orderEvents.AddServiceBusSubscription("basket-order-events", "basket");
orderEvents.AddServiceBusSubscription("notification-order-events", "notification");

// SendGrid settings — real values live in the AppHost's user secrets (Parameters:*).
var sendGridApiKey = builder.AddParameter("sendgrid-api-key", secret: true);
var sendGridFromEmail = builder.AddParameter("sendgrid-from-email");

if (builder.ExecutionContext.IsRunMode)
{
    // Data volumes don't work on ACA for Postgres so only add when running
    postgres.WithDataVolume();
    rabbitmq.WithDataVolume();
}

// Projects
var identity = builder
    .AddProject<Projects.Identity>("identity")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var catalog = builder
    .AddProject<Projects.Catalog>("catalog")
    .WithReference(catalogDb)
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WaitFor(catalogDb)
    .WaitFor(cache)
    .WaitFor(rabbitmq);

var basket = builder
    .AddProject<Projects.Basket>("basket")
    .WithReference(cache)
    .WithReference(catalog)
    .WithReference(rabbitmq)
    .WithReference(serviceBus)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WaitFor(serviceBus);

var orders = builder
    .AddProject<Projects.Orders>("orders")
    .WithReference(ordersDb)
    .WithReference(basket)
    .WithReference(serviceBus)
    .WaitFor(ordersDb)
    .WaitFor(basket)
    .WaitFor(serviceBus);

// Listens for OrderPaid events and emails the customer through SendGrid.
var notification = builder
    .AddProject<Projects.Notification>("notification")
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithEnvironment("SendGrid__ApiKey", sendGridApiKey)
    .WithEnvironment("SendGrid__FromEmail", sendGridFromEmail);

var webapp = builder
    .AddProject<Projects.WebApp>("webapp")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(catalog)
    .WithReference(basket)
    .WithReference(identity)
    .WithReference(orders)
    .WaitFor(catalog)
    .WaitFor(basket)
    .WaitFor(identity)
    .WaitFor(orders);

// React frontend: the Vite dev server proxies /api and /bff to the BFF, which holds the
// login cookie and forwards API calls to the services with the user's JWT attached.
var bff = builder
    .AddProject<Projects.Bff>("bff")
    .WithReference(identity)
    .WithReference(catalog)
    .WithReference(basket)
    .WithReference(orders)
    .WaitFor(identity)
    .WaitFor(catalog)
    .WaitFor(basket)
    .WaitFor(orders);

var frontend = builder
    .AddViteApp("frontend", "../frontend")
    .WithReference(bff)
    .WaitFor(bff)
    .WithExternalHttpEndpoints();

// The payment page (mock or Stripe) sends the customer back to the React app.
orders.WithEnvironment("Payment__WebAppBaseUrl", frontend.GetEndpoint("http"));

builder.Build().Run();
