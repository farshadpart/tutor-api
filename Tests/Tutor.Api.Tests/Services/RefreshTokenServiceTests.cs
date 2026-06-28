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

    [Fact]
    public async Task RevokeAllUserRefreshTokens_WhenMatchingActiveTokensExist_RevokesOnlyMatchingUsersTokensFromIpAndLogsInformation()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var now = DateTimeOffset.UtcNow;
        var ip = "127.0.0.1";
        var otherIp = "127.0.0.2";
        var matchingRefreshTokenHash = "matching-refresh-token-hash";
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
        var activeSameUserSameIp = new RefreshToken(
            user1.Id,
            matchingRefreshTokenHash,
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip);
        var anotherActiveSameUserSameIp = new RefreshToken(
            user1.Id,
            "another-active-same-user-same-ip",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip);
        var activeSameUserOtherIp = new RefreshToken(
            user1.Id,
            "active-same-user-other-ip",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            otherIp);
        var expiredSameUserSameIp = new RefreshToken(
            user1.Id,
            "expired-same-user-same-ip",
            now.AddDays(-7),
            now.AddDays(-1),
            "Mozilla/5.0",
            ip);
        var alreadyRevokedSameUserSameIp = new RefreshToken(
            user1.Id,
            "already-revoked-same-user-same-ip",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip)
        {
            RevokedAt = now.AddHours(-1),
            RevokedByIp = otherIp
        };
        var activeOtherUserSameIp = new RefreshToken(
            user2.Id,
            "active-other-user-same-ip",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip);

        await using (var seedContext = new TutorContext(options))
        {
            seedContext.Users.AddRange(user1, user2);
            seedContext.RefreshTokens.AddRange(
                activeSameUserSameIp,
                anotherActiveSameUserSameIp,
                activeSameUserOtherIp,
                expiredSameUserSameIp,
                alreadyRevokedSameUserSameIp,
                activeOtherUserSameIp);
            await seedContext.SaveChangesAsync();
        }

        var logger = new TestLogger<RefreshTokenService>();
        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);
        var startedAt = DateTimeOffset.UtcNow;

        // Act
        await sut.RevokeAllUserRefreshTokens(matchingRefreshTokenHash, ip);

        // Assert
        await using var assertionContext = new TutorContext(options);
        var refreshTokens = await assertionContext.RefreshTokens
            .ToDictionaryAsync(x => x.TokenHash);

        refreshTokens[matchingRefreshTokenHash].RevokedAt.ShouldNotBeNull();
        refreshTokens[matchingRefreshTokenHash].RevokedAt!.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        refreshTokens[matchingRefreshTokenHash].RevokedByIp.ShouldBe(ip);

        refreshTokens["another-active-same-user-same-ip"].RevokedAt.ShouldNotBeNull();
        refreshTokens["another-active-same-user-same-ip"].RevokedAt!.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        refreshTokens["another-active-same-user-same-ip"].RevokedByIp.ShouldBe(ip);

        refreshTokens["active-same-user-other-ip"].RevokedAt.ShouldBeNull();
        refreshTokens["active-same-user-other-ip"].RevokedByIp.ShouldBeNull();
        refreshTokens["expired-same-user-same-ip"].RevokedAt.ShouldBeNull();
        refreshTokens["expired-same-user-same-ip"].RevokedByIp.ShouldBeNull();
        refreshTokens["already-revoked-same-user-same-ip"].RevokedAt.ShouldBe(alreadyRevokedSameUserSameIp.RevokedAt);
        refreshTokens["already-revoked-same-user-same-ip"].RevokedByIp.ShouldBe(otherIp);
        refreshTokens["active-other-user-same-ip"].RevokedAt.ShouldBeNull();
        refreshTokens["active-other-user-same-ip"].RevokedByIp.ShouldBeNull();

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Information);
        logEntry.Message.ShouldContain("Revoked 2 active refresh token(s) from IP 127.0.0.1 using presented refresh token.");
    }

    [Fact]
    public async Task RevokeAllUserRefreshTokensByUserId_WhenMatchingActiveTokensExist_RevokesOnlyMatchingUsersActiveTokensAndLogsInformation()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var now = DateTimeOffset.UtcNow;
        var ip = "127.0.0.1";
        var otherIp = "127.0.0.2";
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
        var activeSameUser = new RefreshToken(
            user1.Id,
            "active-same-user",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            otherIp);
        var anotherActiveSameUser = new RefreshToken(
            user1.Id,
            "another-active-same-user",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip);
        var expiredSameUser = new RefreshToken(
            user1.Id,
            "expired-same-user",
            now.AddDays(-7),
            now.AddDays(-1),
            "Mozilla/5.0",
            ip);
        var alreadyRevokedSameUser = new RefreshToken(
            user1.Id,
            "already-revoked-same-user",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip)
        {
            RevokedAt = now.AddHours(-1),
            RevokedByIp = otherIp
        };
        var activeOtherUser = new RefreshToken(
            user2.Id,
            "active-other-user",
            now.AddDays(-1),
            now.AddDays(7),
            "Mozilla/5.0",
            ip);

        await using (var seedContext = new TutorContext(options))
        {
            seedContext.Users.AddRange(user1, user2);
            seedContext.RefreshTokens.AddRange(
                activeSameUser,
                anotherActiveSameUser,
                expiredSameUser,
                alreadyRevokedSameUser,
                activeOtherUser);
            await seedContext.SaveChangesAsync();
        }

        var logger = new TestLogger<RefreshTokenService>();
        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);
        var startedAt = DateTimeOffset.UtcNow;

        // Act
        await sut.RevokeAllUserRefreshTokensByUserId(user1.Id, ip);

        // Assert
        await using var assertionContext = new TutorContext(options);
        var refreshTokens = await assertionContext.RefreshTokens
            .ToDictionaryAsync(x => x.TokenHash);

        refreshTokens["active-same-user"].RevokedAt.ShouldNotBeNull();
        refreshTokens["active-same-user"].RevokedAt!.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        refreshTokens["active-same-user"].RevokedByIp.ShouldBe(ip);

        refreshTokens["another-active-same-user"].RevokedAt.ShouldNotBeNull();
        refreshTokens["another-active-same-user"].RevokedAt!.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        refreshTokens["another-active-same-user"].RevokedByIp.ShouldBe(ip);

        refreshTokens["expired-same-user"].RevokedAt.ShouldBeNull();
        refreshTokens["expired-same-user"].RevokedByIp.ShouldBeNull();
        refreshTokens["already-revoked-same-user"].RevokedAt.ShouldBe(alreadyRevokedSameUser.RevokedAt);
        refreshTokens["already-revoked-same-user"].RevokedByIp.ShouldBe(otherIp);
        refreshTokens["active-other-user"].RevokedAt.ShouldBeNull();
        refreshTokens["active-other-user"].RevokedByIp.ShouldBeNull();

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Information);
        logEntry.Message.ShouldContain("Revoked 2 active refresh token(s) for user user-1 from IP 127.0.0.1.");
    }

    [Fact]
    public async Task RevokeAllUserRefreshTokensByUserId_WhenNoTokensMatch_LogsZeroRevokedTokens()
    {
        // Arrange
        var options = CreateDbContextOptions();
        var logger = new TestLogger<RefreshTokenService>();
        await using var context = new TutorContext(options);
        var sut = new RefreshTokenService(context, new AppSettings(), logger);

        // Act
        await sut.RevokeAllUserRefreshTokensByUserId("missing-user", "127.0.0.1");

        // Assert
        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Information);
        logEntry.Message.ShouldContain("Revoked 0 active refresh token(s) for user missing-user from IP 127.0.0.1.");
    }

    private static DbContextOptions<TutorContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<TutorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

}
