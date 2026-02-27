namespace Tutor.Api.Models
{
    public record AppSettings
    {
        public Smtp Smtp { get; set; } = new();
        public JWT Jwt { get; set; } = new();
    }

    public record Smtp
    {
        public string Host { get; set; } = string.Empty;
        public ushort Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
    }

    public record JWT {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public uint AccessTokenExpirationMinutes { get; set; }
        public uint RefreshTokenExpirationDays { get; set; }
        public string SecretKey { get; set; } = string.Empty;
    }
}
