using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services
{
    public class RefreshTokenService(TutorContext TutorContext, AppSettings AppSettings)
    {
        public async Task Add(RefreshToken refreshToken)
        {
            TutorContext.RefreshTokens.Add(refreshToken);
            await TutorContext.SaveChangesAsync();
        }

        public List<RefreshToken> GetRefreshTokens(Func<RefreshToken, bool> func)
        {
            return [..TutorContext.RefreshTokens.Include(x => x.User).Where(func)];
        }

        public async Task RevokeAllUserRefreshTokens(string userId, string ip)
        {
            var activeTokens = await TutorContext.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedByIp = ip;
            }

            await TutorContext.SaveChangesAsync();
        }

        public async Task<RefreshTokenHolder> CreateRefreshToken(User user, string ip, string userAgent)
        {
            var refreshRaw = TokenHelpers.GenerateRefreshToken();
            var refreshHash = TokenHelpers.Sha256(refreshRaw);
            var refreshExp = DateTime.UtcNow.AddDays(AppSettings.Jwt.RefreshTokenExpirationDays);

            var refreshToken = new RefreshToken(user.Id, refreshHash, DateTime.UtcNow, refreshExp, userAgent, ip);
            TutorContext.RefreshTokens.Add(refreshToken);
            await TutorContext.SaveChangesAsync();

            return new RefreshTokenHolder(refreshRaw, refreshExp);
        }

    }
}
