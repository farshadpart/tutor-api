using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Tests.Utility;
using Tutor.Api.Utilities;

namespace Tutor.Api.Tests.Services;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorizedWithoutCreatingRefreshToken()
    {
        var userManager = CreateUserManager();
        var refreshTokenService = Substitute.For<IRefreshTokenService>();
        var sut = CreateService(userManager, refreshTokenService);
        var request = new RequestLogin("missing@example.com", "Password1!");

        userManager.FindByEmailAsync(request.Email).Returns((User?)null);
        userManager.FindByNameAsync(request.Email).Returns((User?)null);

        var result = await sut.Login(request, "127.0.0.1", "test-agent");

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Metadata["MethodName"].ShouldBe("Unauthorized");
        await refreshTokenService.DidNotReceive().CreateRefreshToken(
            Arg.Any<User>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsAccessAndRefreshTokens()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "student@example.com",
            UserName = "student@example.com"
        };
        var userManager = CreateUserManager();
        var refreshTokenService = Substitute.For<IRefreshTokenService>();
        var sut = CreateService(userManager, refreshTokenService);
        var request = new RequestLogin(user.Email, "Password1!");
        var refreshToken = new RefreshTokenHolder("refresh-token", DateTime.UtcNow.AddDays(7));

        userManager.FindByEmailAsync(request.Email).Returns(user);
        userManager.CheckPasswordAsync(user, request.Password).Returns(true);
        userManager.IsEmailConfirmedAsync(user).Returns(true);
        userManager.GetRolesAsync(user).Returns(Array.Empty<string>());
        userManager.GetClaimsAsync(user).Returns([]);
        refreshTokenService
            .CreateRefreshToken(user, "127.0.0.1", "test-agent")
            .Returns(refreshToken);

        var result = await sut.Login(request, "127.0.0.1", "test-agent");

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.Token.ShouldNotBeNullOrWhiteSpace();
        result.Value.RefreshToken.ShouldBe(refreshToken);
        await refreshTokenService.Received(1)
            .CreateRefreshToken(user, "127.0.0.1", "test-agent");
    }

    [Fact]
    public async Task Logout_HashesPresentedTokenAndRevokesMatchingUserTokens()
    {
        const string refreshToken = "raw-refresh-token";
        const string ip = "127.0.0.1";
        var refreshTokenService = Substitute.For<IRefreshTokenService>();
        var sut = CreateService(CreateUserManager(), refreshTokenService);

        await sut.Logout(refreshToken, ip);

        await refreshTokenService.Received(1)
            .RevokeAllUserRefreshTokens(TokenHelpers.Sha256(refreshToken), ip);
    }

    private static AuthenticationService CreateService(
        UserManager<User> userManager,
        IRefreshTokenService refreshTokenService)
    {
        Environment.SetEnvironmentVariable("JwtSecretKey", "unit-test-secret-key-with-at-least-32-chars");

        return new AuthenticationService(
            new AppSettings
            {
                Jwt = new JWT
                {
                    Issuer = "Tutor.Api.Tests",
                    Audience = "Tutor.Api.Tests",
                    AccessTokenExpirationMinutes = 15,
                    RefreshTokenExpirationDays = 7
                }
            },
            userManager,
            Substitute.For<ISubscriptionService>(),
            refreshTokenService,
            new TestLogger<AuthenticationService>());
    }

    private static UserManager<User> CreateUserManager() =>
        Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
}
