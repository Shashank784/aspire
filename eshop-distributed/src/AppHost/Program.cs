
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

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    //.WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

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
    .WithReference(rabbitmq)
    .WaitFor(catalogDb)
    .WaitFor(rabbitmq);

var basket = builder
    .AddProject<Projects.Basket>("basket")
    .WithReference(cache)
    .WithReference(catalog)
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq);

var orders = builder
    .AddProject<Projects.Orders>("orders")
    .WithReference(ordersDb)
    .WithReference(basket)
    .WaitFor(ordersDb)
    .WaitFor(basket);

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

// Stripe sends the customer back to the React app after payment.
orders.WithEnvironment("Stripe__WebAppBaseUrl", frontend.GetEndpoint("http"));

builder.Build().Run();
