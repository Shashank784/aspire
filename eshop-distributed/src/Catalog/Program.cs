
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ProductDbContext>(connectionName: "catalogdb");
builder.Services.AddScoped<ProductService>();

// Blob container for uploaded product images ("product-images" from the AppHost).
builder.AddAzureBlobContainerClient("product-images");
builder.Services.AddSingleton<ProductImageStorage>();

// Redis (the "cache" resource from the AppHost) is the shared L2 cache behind HybridCache.
builder.AddRedisDistributedCache(connectionName: "cache");
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),          // how long an entry lives in Redis
        LocalCacheExpiration = TimeSpan.FromMinutes(1)  // how long this instance keeps it in memory
    };
});
// Service Bus: price changes are published to the "product-events" topic.
builder.AddAzureServiceBusClient("servicebus");

builder.AddJwtValidation();

// GenAI: IChatClient (Microsoft.Extensions.AI) backed by the Ollama "chat" model from the AppHost.
// Our code only talks to IChatClient, so Ollama can later be swapped for OpenAI / Azure OpenAI.
builder.AddOllamaApiClient("chat").AddChatClient();
builder.Services.AddScoped<ProductAIService>();

// Semantic search: IEmbeddingGenerator backed by the Ollama "embedding" model (all-minilm).
// The keyed client keeps it separate from the "chat" client, so each one talks to its own model.
builder.AddKeyedOllamaApiClient("embedding").AddEmbeddingGenerator();

// Vector store (Microsoft.Extensions.VectorData). In-memory for the demo; swap this one line for
// Qdrant / pgvector / Azure AI Search in production. Singletons so the vectors are kept between requests.
builder.Services.AddSingleton<VectorStore>(_ => new InMemoryVectorStore());
builder.Services.AddSingleton<ProductVectorIndex>();

// A local LLM on CPU can take longer than the default 10-second resilience timeout, and retrying
// a slow answer only makes it slower. So the Ollama HttpClient gets a long timeout and no retries.
// ("chat_httpClient" is the name CommunityToolkit.Aspire.OllamaSharp gives the client for "chat".)
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is marked experimental
builder.Services
    .AddHttpClient("chat_httpClient", client => client.Timeout = TimeSpan.FromMinutes(3))
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseMigration();

app.MapProductEndpoints();
app.MapProductAIEndpoints();

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpsRedirection();

app.Run();
