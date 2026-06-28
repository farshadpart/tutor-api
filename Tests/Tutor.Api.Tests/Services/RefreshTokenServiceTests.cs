using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Services;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class RefreshTokenServiceTests
{
    [Fact]
    public async Task Add_WhenRefreshTokenIsProvided_PersistsRefreshTokenAndLogsDebugMessage()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var logger = new TestLogger<RefreshTokenService>();
        var refreshToken = new RefreshToken(
            "user-1",
            "refresh-token-hash",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "Mozilla/5.0",
            "127.0.0.1");

        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);

        // Act
        await sut.Add(refreshToken);

        // Assert
        await using var assertionContext = new TutorContext(options);
        var persistedRefreshToken = await assertionContext.RefreshTokens.SingleAsync();

        persistedRefreshToken.Id.ShouldBe(refreshToken.Id);
        persistedRefreshToken.UserId.ShouldBe(refreshToken.UserId);
        persistedRefreshToken.TokenHash.ShouldBe(refreshToken.TokenHash);
        persistedRefreshToken.CreatedAt.ShouldBe(refreshToken.CreatedAt);
        persistedRefreshToken.ExpiresAt.ShouldBe(refreshToken.ExpiresAt);
        persistedRefreshToken.UserAgent.ShouldBe(refreshToken.UserAgent);
        persistedRefreshToken.CreatedByIp.ShouldBe(refreshToken.CreatedByIp);

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Debug);
        logEntry.Message.ShouldContain("Refresh token persisted for user user-1 from IP 127.0.0.1");
    }

    private static DbContextOptions<TutorContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<TutorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }
}
