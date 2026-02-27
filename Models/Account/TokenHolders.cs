namespace Tutor.Api.Models.Account
{
    public record TokenHolder(AccessTokenHolder AccessToken, RefreshTokenHolder RefreshToken);
    public record AccessTokenHolder(string Token, DateTime Expiration);
    public record RefreshTokenHolder(string Token, DateTime Expiration);
}
