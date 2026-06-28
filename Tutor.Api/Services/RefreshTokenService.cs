using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services
{
    public class RefreshTokenService(TutorContext tutorContext, AppSettings appSettings, ILogger<RefreshTokenService> logger) : IRefreshTokenService
    {
        public async Task Add(RefreshToken refreshToken)
        {
            tutorContext.RefreshTokens.Add(refreshToken);
            await tutorContext.SaveChangesAsync();
            logger.LogDebug(
                "Refresh token persisted for user {UserId} from IP {Ip}; expires at {ExpiresAt}.",
                refreshToken.UserId,
                refreshToken.CreatedByIp,
                refreshToken.ExpiresAt);
        }

        public List<RefreshToken> GetRefreshTokens(Func<RefreshToken, bool> func)
        {
            var refreshTokens = tutorContext.RefreshTokens.Include(x => x.User).Where(func).ToList();
            logger.LogDebug("Refresh token lookup returned {TokenCount} record(s).", refreshTokens.Count);
            return refreshTokens;
        }

        public async Task RevokeAllUserRefreshTokens(string refreshTokenHash, string ip)
        {
            var now = DateTimeOffset.UtcNow;
            var userId = await tutorContext.RefreshTokens
                .Where(t => t.TokenHash == refreshTokenHash)
                .Select(t => t.UserId)
                .SingleOrDefaultAsync();

            List<RefreshToken> refreshTokens = [];
            if (userId is not null)
            {
                refreshTokens = await tutorContext.RefreshTokens
                    .Where(t =>
                        t.UserId == userId &&
                        t.CreatedByIp == ip &&
                        t.RevokedAt == null &&
                        t.ExpiresAt > now)
                    .ToListAsync();

                foreach (var refreshToken in refreshTokens)
                {
                    refreshToken.RevokedAt = now;
                    refreshToken.RevokedByIp = ip;
                }

                await tutorContext.SaveChangesAsync();
            }

            logger.LogInformation(
                "Revoked {RefreshTokenCount} active refresh token(s) from IP {Ip} using presented refresh token.",
                refreshTokens.Count,
                ip);
        }

        public async Task RevokeAllUserRefreshTokensByUserId(string userId, string ip)
        {
            var now = DateTimeOffset.UtcNow;
            var revokedCount = await tutorContext.RefreshTokens
                .Where(t =>
                    t.UserId == userId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedByIp, ip));

            logger.LogInformation(
                "Revoked {RefreshTokenCount} active refresh token(s) for user {UserId} from IP {Ip}.",
                revokedCount,
                userId,
                ip);
        }

        public async Task<RefreshTokenHolder> CreateRefreshToken(User user, string ip, string userAgent)
        {
            var refreshRaw = TokenHelpers.GenerateRefreshToken();
            var refreshHash = TokenHelpers.Sha256(refreshRaw);
            var refreshExp = DateTime.UtcNow.AddDays(appSettings.Jwt.RefreshTokenExpirationDays);

            var refreshToken = new RefreshToken(user.Id, refreshHash, DateTime.UtcNow, refreshExp, userAgent, ip);
            tutorContext.RefreshTokens.Add(refreshToken);
            await tutorContext.SaveChangesAsync();
            logger.LogInformation(
                "Issued refresh token {RefreshTokenId} for user {UserId} from IP {Ip}; expires at {ExpiresAt}.",
                refreshToken.Id,
                user.Id,
                ip,
                refreshExp);

            return new RefreshTokenHolder(refreshRaw, refreshExp);
        }

    }
}
