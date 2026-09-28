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
