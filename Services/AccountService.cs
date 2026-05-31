using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services
{
    public class AccountService
    {
        private readonly AppSettings _appSettings;
        private readonly UserManager<User> _userManager;
        private readonly SubscriptionService _subscriptionService;
        private readonly RefreshTokenService _refreshTokenService;
        private readonly ILogger<AccountService> _logger;

        public AccountService(AppSettings appSettings, UserManager<User> userManager, SubscriptionService subscriptionService, RefreshTokenService refreshTokenService, ILogger<AccountService> logger)
        {
            _appSettings = appSettings;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
            _refreshTokenService = refreshTokenService;
            _logger = logger;
        }

        public async Task<Result<User>> ValidateLoginRequest(RequestLogin requestLogin)
        {
            var identityUser = await _userManager.FindByEmailAsync(requestLogin.Email) ?? await _userManager.FindByNameAsync(requestLogin.Email);

            if (identityUser is null)
            {
                IError authorizationError = new Error("Invalid email or password!")
                    .WithMetadata("MethodName", "Unauthorized");
                return Result.Fail(authorizationError);
            }

            var passwordValid = await _userManager.CheckPasswordAsync(identityUser, requestLogin.Password);
            if (!passwordValid)
            {
                IError authorizationError = new Error("Invalid email or password!")
                    .WithMetadata("MethodName", "Unauthorized");
                return Result.Fail(authorizationError);
            }

            if (!await _userManager.IsEmailConfirmedAsync(identityUser))
            {
                IError authorizationError = new Error("Email not confirmed!")
                    .WithMetadata("MethodName", "Unauthorized");
                return Result.Fail(authorizationError);
            }

            return Result.Ok(identityUser);
        }

        public async Task<AccessTokenHolder> CreateAccessToken(User identityUser)
        {
            var userRoles = await _userManager.GetRolesAsync(identityUser);
            var userClaims = await _userManager.GetClaimsAsync(identityUser);

            var singingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.Jwt.SecretKey));
            var credentials = new SigningCredentials(singingKey, SecurityAlgorithms.HmacSha256);
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Email, identityUser.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, identityUser.UserName ?? string.Empty),
                new Claim(TutorClaimTypes.Id, identityUser.Id)
            ];

            var userSubscriptionGroup = _subscriptionService.GetUserUseableSubscriptionGroup(identityUser.Id);
            if (userSubscriptionGroup is not null)
            {
                claims.Add(new Claim(TutorClaimTypes.SubscriptionGroup, userSubscriptionGroup.Value.ToString()));
            }
            claims.AddRange(userClaims);
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_appSettings.Jwt.AccessTokenExpirationMinutes),
                SigningCredentials = credentials,
                Issuer = _appSettings.Jwt.Issuer,
                Audience = _appSettings.Jwt.Audience,
                IssuedAt = DateTime.UtcNow
            };

            return new AccessTokenHolder(new JsonWebTokenHandler().CreateToken(tokenDescriptor), Expiration: tokenDescriptor.Expires.Value);
        }

        public async Task<Result<TokenHolder>> RefreshAsync(string refreshTokenRaw, string ip, string? userAgent)
        {
            var incomingHash = TokenHelpers.Sha256(refreshTokenRaw);

            var existing = _refreshTokenService.GetRefreshTokens(x => x.TokenHash == incomingHash).FirstOrDefault();

            if (existing == null || existing.User is null)
            {
                IError authorizationError = new Error("Refresh token is not valid!")
                    .WithMetadata("MethodName", "Unauthorized");
                return Result.Fail(authorizationError);
            }

            if (!existing.IsActive)
            {
                IError authorizationError = new Error("Refresh token is not valid!")
                    .WithMetadata("MethodName", "Unauthorized");
                return Result.Fail(authorizationError);
            }

            await _refreshTokenService.RevokeAllUserRefreshTokens(existing.TokenHash, ip);
            var user = existing.User;

            var newRefreshRaw = TokenHelpers.GenerateRefreshToken();
            var newRefreshHash = TokenHelpers.Sha256(newRefreshRaw);
            var newRefreshExp = DateTime.UtcNow.AddDays(_appSettings.Jwt.RefreshTokenExpirationDays);

            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedByIp = ip;
            existing.ReplacedByTokenHash = newRefreshHash;

            await _refreshTokenService.Add(new RefreshToken(user.Id, newRefreshHash, DateTime.UtcNow, newRefreshExp, userAgent, ip));

            var accessTokenResult = await CreateAccessToken(user);

            return Result.Ok(new TokenHolder(accessTokenResult, new RefreshTokenHolder(newRefreshRaw, newRefreshExp)));
        }

        public async Task<Result> ConfirmEmail(string userId, string token)
        {
            var identityUser = await _userManager.FindByIdAsync(userId);
            if (identityUser is null)
            {
                _logger.LogError("Try to confirm invalid userId: {userId}", userId);
                IError authorizationError = new Error("Invalid email or password")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }
            var identityResult = await _userManager.ConfirmEmailAsync(identityUser, token);
            if (!identityResult.Succeeded)
            {
                _logger.LogError("Tried to confirm userId: {userId} with invalide code: {invalidCode}", userId, token);
                IError authorizationError = new Error("Something went wrong!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            return Result.Ok();
        }

        public async Task<Result<(User User, string Token)>> GeneratePasswordResetToken(RequestForgotPassword requestForgotPassword)
        {
            if (!MailAddress.TryCreate(requestForgotPassword.Email, out _))
            {
                IError authorizationError = new Error("The entered email address is not valid!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityUser = await _userManager.FindByEmailAsync(requestForgotPassword.Email);
            if (identityUser?.Email is null)
            {
                _logger.LogInformation("Password reset requested for non-existing email {Email}.", requestForgotPassword.Email);
                return Result.Fail(new Error("Password reset target not found.")
                    .WithMetadata("MethodName", "NotFound"));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Method GeneratePasswordResetTokenAsync failed to generate the reset token for user {UserId}.", identityUser.Id);
                IError authorizationError = new Error("Something went wrong!")
                    .WithMetadata("MethodName", "InternalServerError");
                return Result.Fail(authorizationError);
            }

            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            return Result.Ok((identityUser, encodedToken));
        }

        public async Task<Result> ResetPassword(RequestResetPassword requestResetPassword, string ip)
        {
            if (!MailAddress.TryCreate(requestResetPassword.Email, out _))
            {
                IError authorizationError = new Error("The entered email address is not valid!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityUser = await _userManager.FindByEmailAsync(requestResetPassword.Email);
            if (identityUser is null)
            {
                IError authorizationError = new Error("Invalid password!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(requestResetPassword.Token));
            }
            catch (FormatException)
            {
                _logger.LogError("Failed to generate UTF8 reset password token for userId: {UserId}, from token: {token}", identityUser.Id, requestResetPassword.Token);
                IError authorizationError = new Error("Invalid password reset request!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityResult = await _userManager.ResetPasswordAsync(identityUser, token, requestResetPassword.NewPassword);
            if (!identityResult.Succeeded)
            {
                _logger.LogError("Failed to reset password for user {UserId}. Errors: {@errors}", identityUser.Id, identityResult.Errors);
                IError authorizationError = new Error("Invalid password reset request!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            await _refreshTokenService.RevokeAllUserRefreshTokensByUserId(identityUser.Id, ip);

            return Result.Ok();
        }

        public async Task<Result<(User User, string Token)>> Register(RequestCreateUser requestCreateUser)
        {
            if (!MailAddress.TryCreate(requestCreateUser.Email, out _))
            {
                IError authorizationError = new Error("The entered email address is not valid!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityUser = new User
            {
                UserName = requestCreateUser.Email,
                NormalizedUserName = requestCreateUser.Email.ToUpper(),
                Email = requestCreateUser.Email,
                NormalizedEmail = requestCreateUser.Email.ToUpper()
            };
            var identityResult = await _userManager.CreateAsync(identityUser, requestCreateUser.Password);

            if (!identityResult.Succeeded)
            {
                _logger.LogError("Failed to create user with email {Email}. Errors: {@errors}", requestCreateUser.Email, identityResult.Errors);
                IError authorizationError = new Error("Failed to create the user!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(identityUser);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
                throw new Exception("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
            }

            return Result.Ok((identityUser, token));
        }
    }
}
