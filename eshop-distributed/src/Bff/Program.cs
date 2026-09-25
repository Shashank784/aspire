using Bff.Authentication;
using Bff.Endpoints;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "eshop.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // This is an API for the React app — answer with status codes, never redirect to a login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddHttpClient<IdentityApiClient>(client =>
{
    client.BaseAddress = new("https+http://identity");
});

builder.Services.AddHttpForwarderWithServiceDiscovery();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// Cookie-authenticated APIs need CSRF protection: a cross-site page can't add a custom
// header without a CORS preflight (which we never allow), so require one on every write.
app.Use(async (context, next) =>
{
    var isApiCall = context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/bff");
    var isWrite = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);

    if (isApiCall && isWrite && context.Request.Headers["X-CSRF"] != "1")
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    await next();
});

// Access tokens only live 15 minutes; refresh them transparently before forwarding.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && context.User.Identity?.IsAuthenticated == true)
    {
        var session = context.RequestServices.GetRequiredService<AuthSession>();
        await session.EnsureFreshTokenAsync(context);
    }

    await next();
});

app.MapAuthEndpoints();
app.MapApiForwarders();

// In production the built React app (frontend/dist) is copied into wwwroot and served from here.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
