using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Mail;
using System.Text;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services
{
    public class AccountService(
        UserManager<User> userManager,
        IRefreshTokenService refreshTokenService,
        IEmailSender<User> emailSender,
        ILogger<AccountService> logger)
    {
        public async Task<Result> RegisterAndSendConfirmation(RequestCreateUser requestCreateUser, string confirmationEndpoint)
        {
            var registerResult = await Register(requestCreateUser);
            if (registerResult.IsFailed)
            {
                return Result.Fail(registerResult.Errors);
            }

            var (user, token) = registerResult.Value;
            if (user.Email is null)
            {
                logger.LogError("User {UserId} email is null, cannot send confirmation link.", user.Id);
                throw new Exception("Something went wrong!");
            }

            var confirmationLink = QueryHelpers.AddQueryString(
                confirmationEndpoint,
                new Dictionary<string, string?> { ["userId"] = user.Id, ["token"] = token }
            );

            await emailSender.SendConfirmationLinkAsync(user, user.Email, confirmationLink);
            logger.LogInformation(
                "Registration completed for user {UserId}; confirmation email sent to {Email}.",
                user.Id,
                user.Email
            );
            return Result.Ok();
        }

        public async Task<Result> SendPasswordResetEmail(RequestForgotPassword request)
        {
            var tokenResult = await GeneratePasswordResetToken(request);
            if (tokenResult.IsFailed)
            {
                var isNotFound = tokenResult.Errors.Any(error =>
                    error.Metadata.TryGetValue("MethodName", out var methodName) &&
                    methodName as string == "NotFound");

                return isNotFound ? Result.Ok() : Result.Fail(tokenResult.Errors);
            }

            var (user, token) = tokenResult.Value;
            if (user.Email is null)
            {
                logger.LogError("User {UserId} email is null, cannot send password reset link.", user.Id);
                return Result.Fail(new Error("Something went wrong!")
                    .WithMetadata("MethodName", "InternalServerError"));
            }

            await emailSender.SendPasswordResetCodeAsync(user, user.Email, token);
            logger.LogInformation("Password reset token generated and emailed for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ConfirmEmail(string userId, string token)
        {
            var identityUser = await userManager.FindByIdAsync(userId);
            if (identityUser is null)
            {
                logger.LogWarning("Email confirmation rejected for invalid userId {UserId}.", userId);
                IError authorizationError = new Error("Invalid email or password")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }
            var identityResult = await userManager.ConfirmEmailAsync(identityUser, token);
            if (!identityResult.Succeeded)
            {
                logger.LogWarning("Email confirmation rejected for user {UserId}. Errors: {@Errors}", userId, identityResult.Errors);
                throw new Exception("Failed to confirm the user");
            }

            logger.LogInformation("Email confirmation succeeded for user {UserId}.", userId);
            return Result.Ok();
        }

        public async Task<Result<(User User, string Token)>> GeneratePasswordResetToken(RequestForgotPassword requestForgotPassword)
        {
            if (!MailAddress.TryCreate(requestForgotPassword.Email, out _))
            {
                logger.LogWarning("Password reset rejected because email address is invalid: {Email}.", requestForgotPassword.Email);
                IError authorizationError = new Error("The entered email address is not valid!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityUser = await userManager.FindByEmailAsync(requestForgotPassword.Email);
            if (identityUser?.Email is null)
            {
                logger.LogWarning("Password reset requested for non-existing email {Email}.", requestForgotPassword.Email);
                return Result.Fail(new Error("Password reset target not found.")
                    .WithMetadata("MethodName", "BadRequest"));
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(identityUser);
            if (string.IsNullOrEmpty(token))
            {
                logger.LogError("Method GeneratePasswordResetTokenAsync failed to generate the reset token for user {UserId}.", identityUser.Id);
                throw new Exception("Method GeneratePasswordResetTokenAsync failed to generate the reset token");
            }

            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            logger.LogInformation("Generated password reset token for user {UserId}.", identityUser.Id);
            return Result.Ok((identityUser, encodedToken));
        }

        public async Task<Result> ResetPassword(RequestResetPassword requestResetPassword, string ip)
        {
            if (!MailAddress.TryCreate(requestResetPassword.Email, out _))
            {
                logger.LogWarning("Password reset rejected from IP {Ip}: invalid email {Email}.", ip, requestResetPassword.Email);
                IError authorizationError = new Error("The entered email address is not valid!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityUser = await userManager.FindByEmailAsync(requestResetPassword.Email);
            if (identityUser is null)
            {
                logger.LogWarning("Password reset rejected from IP {Ip}: no user found for {Email}.", ip, requestResetPassword.Email);
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
                logger.LogWarning("Password reset rejected for user {UserId} from IP {Ip}: reset token was not valid Base64Url.", identityUser.Id, ip);
                IError authorizationError = new Error("Invalid password reset request!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            var identityResult = await userManager.ResetPasswordAsync(identityUser, token, requestResetPassword.NewPassword);
            if (!identityResult.Succeeded)
            {
                logger.LogWarning("Password reset rejected for user {UserId} from IP {Ip}. Errors: {@Errors}", identityUser.Id, ip, identityResult.Errors);
                IError authorizationError = new Error("Invalid password reset request!")
                    .WithMetadata("MethodName", "BadRequest");
                return Result.Fail(authorizationError);
            }

            await refreshTokenService.RevokeAllUserRefreshTokensByUserId(identityUser.Id, ip);

            logger.LogInformation("Password reset succeeded for user {UserId} from IP {Ip}.", identityUser.Id, ip);
            return Result.Ok();
        }

        private async Task<Result<(User User, string Token)>> Register(RequestCreateUser requestCreateUser)
        {
            if (!MailAddress.TryCreate(requestCreateUser.Email, out _))
            {
                logger.LogWarning("Registration rejected because email address is invalid: {Email}.", requestCreateUser.Email);
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
            var identityResult = await userManager.CreateAsync(identityUser, requestCreateUser.Password);

            if (!identityResult.Succeeded)
            {
                logger.LogError("Failed to create user with email {Email}. Errors: {@errors}", requestCreateUser.Email, identityResult.Errors);
                throw new Exception("User creation failed.");
            }

            var token = await userManager.GenerateEmailConfirmationTokenAsync(identityUser);
            if (string.IsNullOrEmpty(token))
            {
                logger.LogError("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token for user {UserId}.", identityUser.Id);
                throw new Exception("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
            }

            logger.LogInformation("Registered user {UserId} with email {Email}; email confirmation token generated.", identityUser.Id, identityUser.Email);
            return Result.Ok((identityUser, token));
        }
    }
}
