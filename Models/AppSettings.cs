using Tutor.Api.Models;

namespace Tutor.Api.Models
{
    public record AppSettings
    {
        public MailJet MailJet { get; set; } = new();
        public JWT Jwt { get; set; } = new();
    }

    public record MailJet
    {
        public string MailJetSendEndpoint { get; set; } = string.Empty;
        public MailJetCredentials MailCredentials { get; set; } = new();
        public MailConfiguration MailConfiguration { get; set; } = new();
    }

    public record MailConfiguration
    {
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Tutor";
    }

    public record MailJetCredentials
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
    }

    public record JWT {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public uint AccessTokenExpirationMinutes { get; set; }
        public uint RefreshTokenExpirationDays { get; set; }
        public string SecretKey { get; set; } = string.Empty;
    }
}

public static class AppSettingsUtilities
{
    public static MailJetCredentials GetMailJetCredentials(this AppSettings appSettings)
    {
        return new MailJetCredentials
        {
            ApiKey = Environment.GetEnvironmentVariable("MailJetApiKey") ?? throw new NullReferenceException("MailJetApiKey is missing"),
            ApiSecret = Environment.GetEnvironmentVariable("MailJetApiSecretKey")  ?? throw new NullReferenceException("MailJetApiSecretKey is missing"),
        };
    }
}
