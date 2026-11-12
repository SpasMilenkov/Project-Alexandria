namespace Alexandria.Data.Models;

public class RefreshToken : IBase
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Tokens rotate on every refresh, so these carry the session across rotations
    public DateTime SessionStartedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    // IBase properties
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}