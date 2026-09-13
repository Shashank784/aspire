namespace Identity.Endpoints;

public record RegisterRequest(string Email, string Password, string? FullName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, IEnumerable<string> Roles);

public static class AuthEndpoints
{
    private const int RefreshTokenLifetimeDays = 7;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        // Registers a new account. Always assigned the "User" role — becoming
        // an Admin is an out-of-band action (seeded default admin, or promoted manually).
        group.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> userManager, TokenService tokenService, ApplicationDbContext db) =>
        {
            var existing = await userManager.FindByEmailAsync(request.Email);
            if (existing is not null)
            {
                return Results.Conflict("A user with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            }

            await userManager.AddToRoleAsync(user, "User");

            return Results.Ok(await IssueTokensAsync(user, userManager, tokenService, db));
        })
        .WithName("Register")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status409Conflict);

        // Validates credentials and issues an access + refresh token pair.
        group.MapPost("/login", async (LoginRequest request, UserManager<ApplicationUser> userManager, TokenService tokenService, ApplicationDbContext db) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await IssueTokensAsync(user, userManager, tokenService, db));
        })
        .WithName("Login")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Exchanges a still-valid refresh token for a new access token (rotates the refresh token).
        group.MapPost("/refresh", async (RefreshRequest request, UserManager<ApplicationUser> userManager, TokenService tokenService, ApplicationDbContext db) =>
        {
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
            if (stored is null || !stored.IsActive)
            {
                return Results.Unauthorized();
            }

            var user = await userManager.FindByIdAsync(stored.UserId);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            stored.IsRevoked = true;

            return Results.Ok(await IssueTokensAsync(user, userManager, tokenService, db));
        })
        .WithName("Refresh")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, UserManager<ApplicationUser> userManager, TokenService tokenService, ApplicationDbContext db)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = TokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays)
        });
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, expiresAtUtc, refreshToken, roles);
    }
}
