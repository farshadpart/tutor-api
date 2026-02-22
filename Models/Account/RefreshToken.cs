namespace Tutor.Api.Models.Account
{
    public class RefreshToken
    {
        public RefreshToken(){}

        public RefreshToken(string userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiredAt, string? userAgent, string ip)
        {
            UserId = userId;
            TokenHash = tokenHash;
            CreatedAt = createdAt;
            ExpiresAt = expiredAt;
            UserAgent = userAgent;
            CreatedByIp = ip;
        }

        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = new User();
        public string TokenHash { get; set; } = default!;

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }

        public string? CreatedByIp { get; private set; }
        public string? RevokedByIp { get; set; }
        public string? UserAgent { get; private set; }

        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
        public bool IsActive => RevokedAt == null && !IsExpired;
    }
}
