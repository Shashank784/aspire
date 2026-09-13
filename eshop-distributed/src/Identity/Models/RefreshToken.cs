namespace Identity.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }

    public bool IsActive => !IsRevoked && ExpiresAtUtc > DateTime.UtcNow;
}
