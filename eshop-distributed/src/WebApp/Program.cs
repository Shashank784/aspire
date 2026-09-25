using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebApp;
using WebApp.ApiClients;
using WebApp.Authentication;
using WebApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddTransient<BearerTokenHandler>();

builder.Services.AddHttpClient<IdentityApiClient>(client =>
{
    client.BaseAddress = new("https+http://identity");
});

builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    client.BaseAddress = new("https+http://catalog");
})
.AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<BasketApiClient>(client =>
{
    client.BaseAddress = new("https+http://basket");
})
.AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<OrdersApiClient>(client =>
{
    client.BaseAddress = new("https+http://orders");
})
.AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddScoped<BasketActions>();

builder.AddRedisOutputCache("cache");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseOutputCache();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/account/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

// A plain <a href> can't carry a Bearer token, so the invoice download goes through this
// cookie-authenticated endpoint, which fetches the PDF from Orders (via BearerTokenHandler,
// using the JWT stashed in the cookie) and streams it back to the browser.
app.MapGet("/downloads/invoice/{id:int}", async (int id, OrdersApiClient ordersApiClient) =>
{
    var pdfBytes = await ordersApiClient.DownloadInvoicePdf(id);
    return pdfBytes is not null
        ? Results.File(pdfBytes, "application/pdf", $"invoice-{id}.pdf")
        : Results.NotFound();
})
.RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
