using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services;

public class AuthenticationService(
    AppSettings appSettings,
    UserManager<User> userManager,
    ISubscriptionService subscriptionService,
    IRefreshTokenService refreshTokenService,
    ILogger<AuthenticationService> logger)
{
    public async Task<Result<TokenHolder>> Login(RequestLogin requestLogin, string ip, string userAgent)
    {
        var userResult = await ValidateLoginRequest(requestLogin);
        if (userResult.IsFailed)
        {
            return Result.Fail<TokenHolder>(userResult.Errors);
        }

        var accessToken = await CreateAccessToken(userResult.Value);
        var refreshToken = await refreshTokenService.CreateRefreshToken(userResult.Value, ip, userAgent);

        logger.LogInformation("Login succeeded for user {UserId} from IP {Ip}.", userResult.Value.Id, ip);
        return Result.Ok(new TokenHolder(accessToken, refreshToken));
    }

    public async Task Logout(string refreshToken, string ip)
    {
        var refreshTokenHash = TokenHelpers.Sha256(refreshToken);
        await refreshTokenService.RevokeAllUserRefreshTokens(refreshTokenHash, ip);
        logger.LogInformation("Logout completed from IP {Ip}.", ip);
    }

    private async Task<Result<User>> ValidateLoginRequest(RequestLogin requestLogin)
    {
        logger.LogDebug("Validating login request for {Email}.", requestLogin.Email);
        var identityUser = await userManager.FindByEmailAsync(requestLogin.Email) ?? await userManager.FindByNameAsync(requestLogin.Email);

        if (identityUser is null)
        {
            logger.LogWarning("Login rejected for {Email}: user was not found.", requestLogin.Email);
            return Result.Fail(new Error("Invalid email or password!").WithMetadata("MethodName", "Unauthorized"));
        }

        if (!await userManager.CheckPasswordAsync(identityUser, requestLogin.Password))
        {
            logger.LogWarning("Login rejected for user {UserId}: invalid password.", identityUser.Id);
            return Result.Fail(new Error("Invalid email or password!").WithMetadata("MethodName", "Unauthorized"));
        }

        if (!await userManager.IsEmailConfirmedAsync(identityUser))
        {
            logger.LogWarning("Login rejected for user {UserId}: email is not confirmed.", identityUser.Id);
            return Result.Fail(new Error("Email not confirmed!").WithMetadata("MethodName", "Unauthorized"));
        }

        logger.LogInformation("Login request validated for user {UserId}.", identityUser.Id);
        return Result.Ok(identityUser);
    }

    public async Task<AccessTokenHolder> CreateAccessToken(User identityUser)
    {
        var userRoles = await userManager.GetRolesAsync(identityUser);
        var userClaims = await userManager.GetClaimsAsync(identityUser);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EnvironmentUtility.GetJwtSecretKey()));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(ClaimTypes.Email, identityUser.Email ?? string.Empty),
            new(ClaimTypes.Name, identityUser.UserName ?? string.Empty),
            new(TutorClaimTypes.Id, identityUser.Id)
        ];

        var subscriptionGroup = subscriptionService.GetUserUseableSubscriptionGroup(identityUser.Id);
        if (subscriptionGroup is not null)
        {
            claims.Add(new Claim(TutorClaimTypes.SubscriptionGroup, subscriptionGroup.Value.ToString()));
        }

        claims.AddRange(userClaims);
        claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(appSettings.Jwt.AccessTokenExpirationMinutes),
            SigningCredentials = credentials,
            Issuer = appSettings.Jwt.Issuer,
            Audience = appSettings.Jwt.Audience,
            IssuedAt = DateTime.UtcNow
        };

        return new AccessTokenHolder(new JsonWebTokenHandler().CreateToken(descriptor), descriptor.Expires.Value);
    }

    public async Task<Result<TokenHolder>> RefreshAsync(string refreshTokenRaw, string ip, string? userAgent)
    {
        var incomingHash = TokenHelpers.Sha256(refreshTokenRaw);
        var existing = refreshTokenService.GetRefreshTokens(x => x.TokenHash == incomingHash).FirstOrDefault();

        if (existing?.User is null || !existing.IsActive)
        {
            logger.LogWarning("Refresh token rejected from IP {Ip}.", ip);
            return Result.Fail(new Error("Refresh token is not valid!").WithMetadata("MethodName", "Unauthorized"));
        }

        await refreshTokenService.RevokeAllUserRefreshTokens(existing.TokenHash, ip);
        var newRefreshRaw = TokenHelpers.GenerateRefreshToken();
        var newRefreshHash = TokenHelpers.Sha256(newRefreshRaw);
        var newRefreshExpiration = DateTime.UtcNow.AddDays(appSettings.Jwt.RefreshTokenExpirationDays);

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ip;
        existing.ReplacedByTokenHash = newRefreshHash;

        await refreshTokenService.Add(
            new RefreshToken(
                existing.User.Id,
                newRefreshHash,
                DateTime.UtcNow,
                newRefreshExpiration,
                userAgent,
                ip
            )
        );

        var accessToken = await CreateAccessToken(existing.User);
        return Result.Ok(new TokenHolder(accessToken, new RefreshTokenHolder(newRefreshRaw, newRefreshExpiration)));
    }
}
