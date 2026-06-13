using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services
{
    public class RefreshTokenService(TutorContext TutorContext, AppSettings AppSettings, ILogger<RefreshTokenService> Logger)
    {
        public async Task Add(RefreshToken refreshToken)
        {
            TutorContext.RefreshTokens.Add(refreshToken);
            await TutorContext.SaveChangesAsync();
            Logger.LogDebug(
                "Refresh token persisted for user {UserId} from IP {Ip}; expires at {ExpiresAt}.",
                refreshToken.UserId,
                refreshToken.CreatedByIp,
                refreshToken.ExpiresAt);
        }

        public List<RefreshToken> GetRefreshTokens(Func<RefreshToken, bool> func)
        {
            var refreshTokens = TutorContext.RefreshTokens.Include(x => x.User).Where(func).ToList();
            Logger.LogDebug("Refresh token lookup returned {TokenCount} record(s).", refreshTokens.Count);
            return refreshTokens;
        }

        public async Task RevokeAllUserRefreshTokens(string refreshTokenHash, string ip)
        {
            var now = DateTime.UtcNow;
            var revokedCount = await TutorContext.RefreshTokens
                .Where(t =>
                    t.CreatedByIp == ip &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > now &&
                    TutorContext.RefreshTokens.Any(x =>
                        x.TokenHash == refreshTokenHash &&
                        x.UserId == t.UserId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedByIp, ip));

            Logger.LogInformation(
                "Revoked {RefreshTokenCount} active refresh token(s) from IP {Ip} using presented refresh token.",
                revokedCount,
                ip);
        }

        public async Task RevokeAllUserRefreshTokensByUserId(string userId, string ip)
        {
            var now = DateTime.UtcNow;
            var revokedCount = await TutorContext.RefreshTokens
                .Where(t =>
                    t.UserId == userId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedByIp, ip));

            Logger.LogInformation(
                "Revoked {RefreshTokenCount} active refresh token(s) for user {UserId} from IP {Ip}.",
                revokedCount,
                userId,
                ip);
        }

        public async Task<RefreshTokenHolder> CreateRefreshToken(User user, string ip, string userAgent)
        {
            var refreshRaw = TokenHelpers.GenerateRefreshToken();
            var refreshHash = TokenHelpers.Sha256(refreshRaw);
            var refreshExp = DateTime.UtcNow.AddDays(AppSettings.Jwt.RefreshTokenExpirationDays);

            var refreshToken = new RefreshToken(user.Id, refreshHash, DateTime.UtcNow, refreshExp, userAgent, ip);
            TutorContext.RefreshTokens.Add(refreshToken);
            await TutorContext.SaveChangesAsync();
            Logger.LogInformation(
                "Issued refresh token {RefreshTokenId} for user {UserId} from IP {Ip}; expires at {ExpiresAt}.",
                refreshToken.Id,
                user.Id,
                ip,
                refreshExp);

            return new RefreshTokenHolder(refreshRaw, refreshExp);
        }

    }
}
