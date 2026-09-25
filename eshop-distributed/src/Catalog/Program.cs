
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
builder.Services.AddMassTransitWithAssemblies(Assembly.GetExecutingAssembly());

builder.AddJwtValidation();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseMigration();

app.MapProductEndpoints();

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpsRedirection();

app.Run();
