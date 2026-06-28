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
    public async Task GetRefreshTokens_WhenMatchingRefreshTokensExist_ReturnsFilteredTokensWithUsersAndLogsDebugMessage()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var user1 = new User
        {
            Id = "user-1",
            UserName = "user1@example.com",
            Email = "user1@example.com"
        };
        var user2 = new User
        {
            Id = "user-2",
            UserName = "user2@example.com",
            Email = "user2@example.com"
        };
        var matchingRefreshToken = new RefreshToken(
            user1.Id,
            "matching-refresh-token-hash",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "Mozilla/5.0",
            "127.0.0.1");
        var nonMatchingRefreshToken = new RefreshToken(
            user2.Id,
            "non-matching-refresh-token-hash",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "Mozilla/5.0",
            "127.0.0.2");

        await using (var seedContext = new TutorContext(options))
        {
            seedContext.Users.AddRange(user1, user2);
            seedContext.RefreshTokens.AddRange(matchingRefreshToken, nonMatchingRefreshToken);
            await seedContext.SaveChangesAsync();
        }

        var logger = new TestLogger<RefreshTokenService>();
        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);

        // Act
        var refreshTokens = sut.GetRefreshTokens(x => x.UserId == user1.Id);

        // Assert
        var refreshToken = refreshTokens.ShouldHaveSingleItem();
        refreshToken.Id.ShouldBe(matchingRefreshToken.Id);
        refreshToken.TokenHash.ShouldBe(matchingRefreshToken.TokenHash);
        refreshToken.User.ShouldNotBeNull();
        refreshToken.User.Id.ShouldBe(user1.Id);
        refreshToken.User.Email.ShouldBe(user1.Email);

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Debug);
        logEntry.Message.ShouldContain("Refresh token lookup returned 1 record(s).");
    }

    [Fact]
    public async Task GetRefreshTokens_WhenNoRefreshTokensMatch_ReturnsEmptyListAndLogsDebugMessage()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var refreshToken = new RefreshToken(
            "user-1",
            "refresh-token-hash",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "Mozilla/5.0",
            "127.0.0.1");

        await using (var seedContext = new TutorContext(options))
        {
            seedContext.RefreshTokens.Add(refreshToken);
            await seedContext.SaveChangesAsync();
        }

        var logger = new TestLogger<RefreshTokenService>();
        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);

        // Act
        var refreshTokens = sut.GetRefreshTokens(x => x.UserId == "missing-user");

        // Assert
        refreshTokens.ShouldBeEmpty();

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Debug);
        logEntry.Message.ShouldContain("Refresh token lookup returned 0 record(s).");
    }

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
