
var builder = DistributedApplication.CreateBuilder(args);

// Backing Services
// Locally these run as containers; on deploy they become Azure Database for PostgreSQL
// and Azure Managed Redis. Password / access-key auth keeps the plain Npgsql and
// StackExchange.Redis clients working without Entra ID token code.
var postgres = builder
    .AddAzurePostgresFlexibleServer("postgres")
    .WithPasswordAuthentication()
    .RunAsContainer(container => container
        .WithPgAdmin()
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var ordersDb = postgres.AddDatabase("ordersdb");

var cache = builder
    .AddAzureManagedRedis("cache")
    .WithAccessKeyAuthentication()
    .RunAsContainer(container => container
        .WithRedisInsight()
        .WithLifetime(ContainerLifetime.Persistent));

// Azure Service Bus (local emulator) carries all integration events. The emulator can't
// create entities at runtime, so the topics and their subscriptions are declared here.
var serviceBus = builder
    .AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

var orderEvents = serviceBus.AddServiceBusTopic("order-events");
orderEvents.AddServiceBusSubscription("basket-order-events", "basket");
orderEvents.AddServiceBusSubscription("notification-order-events", "notification");

var productEvents = serviceBus.AddServiceBusTopic("product-events");
productEvents.AddServiceBusSubscription("basket-product-events", "basket");

// Azure Storage (local Azurite emulator) holds uploaded product images.
// The data volume keeps the images when the container is recreated.
var storage = builder
    .AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

var productImages = storage.AddBlobContainer("product-images");

// SendGrid settings — real values live in the AppHost's user secrets (Parameters:*).
var sendGridApiKey = builder.AddParameter("sendgrid-api-key", secret: true);
var sendGridFromEmail = builder.AddParameter("sendgrid-from-email");

// Key that Identity signs JWTs with and the APIs validate them with. Overrides Jwt:SigningKey
// from appsettings; the real value lives in user secrets locally and as a secret in Azure.
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

// GenAI backing service: Ollama runs a local LLM in a container, just like Postgres or Redis.
// The model is downloaded automatically on the first run (~2 GB) and kept in the data volume.
// "chat" is the connection name the Catalog service uses to get an IChatClient.
// llama3.2 (3b) follows the "only use our products" rules much better than llama3.2:1b.
var ollama = builder
    .AddOllama("ollama")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var chatModel = ollama.AddModel("chat", "llama3.2");

// Embedding model for semantic search. all-minilm (~45 MB) turns text into 384 numbers (a vector);
// texts with similar meaning get similar vectors. "embedding" is the connection name in Catalog.
var embeddingModel = ollama.AddModel("embedding", "all-minilm");

// Projects
var identity = builder
    .AddProject<Projects.Identity>("identity")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithReference(identityDb)
    .WaitFor(identityDb);

var catalog = builder
    .AddProject<Projects.Catalog>("catalog")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithReference(catalogDb)
    .WithReference(cache)
    .WithReference(serviceBus)
    .WithReference(productImages)
    // No WaitFor here: the model download can take minutes on the first run, and the rest of
    // the shop should not wait for it. Only the AI endpoints need the model.
    .WithReference(chatModel)
    .WithReference(embeddingModel)
    .WaitFor(catalogDb)
    .WaitFor(productImages)
    .WaitFor(cache)
    .WaitFor(serviceBus);

var basket = builder
    .AddProject<Projects.Basket>("basket")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithReference(cache)
    .WithReference(catalog)
    .WithReference(serviceBus)
    .WaitFor(cache)
    .WaitFor(serviceBus);

var orders = builder
    .AddProject<Projects.Orders>("orders")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
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

// React frontend: locally the Vite dev server proxies /api and /bff to the BFF, which holds
// the login cookie and forwards API calls to the services with the user's JWT attached.
// On deploy the built app is copied into the BFF's wwwroot, so the BFF is the public site.
var frontend = builder.AddViteApp("frontend", "../frontend");

var bff = builder
    .AddProject<Projects.Bff>("bff")
    .WithReference(identity)
    .WithReference(catalog)
    .WithReference(basket)
    .WithReference(orders)
    .WaitFor(identity)
    .WaitFor(catalog)
    .WaitFor(basket)
    .WaitFor(orders)
    .PublishWithContainerFiles(frontend, "./wwwroot");

if (builder.ExecutionContext.IsPublishMode)
{
    bff.WithExternalHttpEndpoints();

    // azd can't resolve CACHE_URI (a Key Vault secret and an output in one value), so drop it.
    // The services only read ConnectionStrings__cache.
    catalog.WithEnvironment(ctx => ctx.EnvironmentVariables.Remove("CACHE_URI"));
    basket.WithEnvironment(ctx => ctx.EnvironmentVariables.Remove("CACHE_URI"));
}

frontend
    .WithReference(bff)
    .WaitFor(bff)
    .WithExternalHttpEndpoints();

builder.Build().Run();
