using Notification;
using SendGrid.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.AddAzureServiceBusClient("servicebus");

// SendGrid:ApiKey and SendGrid:FromEmail come from the AppHost's secret parameters.
builder.Services.AddSendGrid(options => options.ApiKey = builder.Configuration["SendGrid:ApiKey"]
    ?? throw new InvalidOperationException("SendGrid:ApiKey is not configured."));

builder.Services.AddHostedService<OrderPaidEmailListener>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.Run();
