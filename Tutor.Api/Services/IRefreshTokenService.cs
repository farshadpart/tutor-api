using Tutor.Api.Models.Account;

namespace Tutor.Api.Services
{
    public interface IRefreshTokenService
    {
        Task Add(RefreshToken refreshToken);
        List<RefreshToken> GetRefreshTokens(Func<RefreshToken, bool> func);
        Task RevokeAllUserRefreshTokens(string refreshTokenHash, string ip);
        Task RevokeAllUserRefreshTokensByUserId(string userId, string ip);
        Task<RefreshTokenHolder> CreateRefreshToken(User user, string ip, string userAgent);
    }
}
