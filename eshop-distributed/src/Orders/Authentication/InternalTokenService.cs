using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Orders.Authentication;

// The Stripe webhook call has no logged-in user attached to it (Stripe is calling us
// directly, not the browser), so there's no user JWT to forward to Basket when we need
// to clear the cart after payment. Instead we mint a short-lived internal token, signed
// with the same shared Jwt:SigningKey Identity/Catalog/Basket/Orders all trust, carrying
// the "Admin" role so Basket's ownership check (OwnsBasketFor) lets it touch any basket.
public class InternalTokenService(IConfiguration configuration)
{
    public string CreateServiceToken()
    {
        var jwtSection = configuration.GetSection("Jwt");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "orders-service"),
            new(JwtRegisteredClaimNames.UniqueName, "orders-service"),
            new(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
