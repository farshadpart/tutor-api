using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;
using Tutor.Api.Utilities;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController(AccountService AccountService, RefreshTokenService RefreshTokenService, ILogger<AccountController> Logger, IEmailSender<User> EmailSender) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RequestLogin requestLogin)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();
            Logger.LogInformation("Login requested for {Email} from IP {Ip}.", requestLogin.Email, ip);

            var userResult = await AccountService.ValidateLoginRequest(requestLogin);
            if (userResult.IsFailed)
            {
                Logger.LogWarning("Login failed for {Email} from IP {Ip}.", requestLogin.Email, ip);
                return ToActionResult(userResult);
            }

            var accessToken = await AccountService.CreateAccessToken(userResult.Value);
            var refreshToken = await RefreshTokenService.CreateRefreshToken(userResult.Value, ip, ua);

            Logger.LogInformation("Login succeeded for user {UserId} from IP {Ip}.", userResult.Value.Id, ip);
            return Ok(new TokenHolder(accessToken, refreshToken));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RequestCreateUser requestCreateUser)
        {
            Logger.LogInformation("Registration requested for {Email}.", requestCreateUser.Email);
            var registerResult = await AccountService.Register(requestCreateUser);
            if (registerResult.IsFailed)
            {
                Logger.LogWarning("Registration failed for {Email}.", requestCreateUser.Email);
                return ToActionResult(registerResult);
            }

            var (identityUser, token) = registerResult.Value;
            var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = identityUser.Id, token }, Request.Scheme);
            if (confirmationLink is null)
            {
                Logger.LogError("Failed to generate confirmation link for user {UserId}", identityUser.Id);
                return StatusCode(500, "Something went wrong!");
            }
            
            if(identityUser.Email is null) {
                Logger.LogError("User {UserId} email is null, cannot send confirmation link.", identityUser.Id);
                return StatusCode(500, "Something went wrong!");
            }
            await EmailSender.SendConfirmationLinkAsync(identityUser, identityUser.Email, confirmationLink);

            Logger.LogInformation("Registration completed for user {UserId}; confirmation email sent to {Email}.", identityUser.Id, identityUser.Email);
            return Ok("User registered successfully! Please check your email to confirm your account.");
        }

        [HttpGet("confirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            Logger.LogInformation("Email confirmation requested for user {UserId}.", userId);
            var confirmResult = await AccountService.ConfirmEmail(userId, token);
            if (confirmResult.IsFailed)
            {
                Logger.LogWarning("Email confirmation failed for user {UserId}.", userId);
                return ToActionResult(confirmResult);
            }

            Logger.LogInformation("Email confirmed for user {UserId}.", userId);
            return Ok("Email confirmed successfully!");
        }

        [HttpPost("forgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] RequestForgotPassword requestForgotPassword)
        {
            Logger.LogInformation("Password reset requested for {Email}.", requestForgotPassword.Email);
            var resetTokenResult = await AccountService.GeneratePasswordResetToken(requestForgotPassword);
            if (resetTokenResult.IsFailed)
            {
                if (resetTokenResult.Errors[0].Metadata.TryGetValue("MethodName", out var methodName) &&
                    methodName as string == "NotFound")
                {
                    Logger.LogWarning("Password reset response normalized for non-existing email {Email}.", requestForgotPassword.Email);
                    return Ok("If an account exists for this email, a password reset link has been sent.");
                }

                Logger.LogWarning("Password reset request failed for {Email}.", requestForgotPassword.Email);
                return ToActionResult(resetTokenResult);
            }

            var (identityUser, token) = resetTokenResult.Value;
            if (identityUser.Email is null)
            {
                Logger.LogError("User {UserId} email is null, cannot send password reset link.", identityUser.Id);
                return StatusCode(500, "Something went wrong!");
            }
            
            await EmailSender.SendPasswordResetCodeAsync(identityUser, identityUser.Email, token);

            Logger.LogInformation("Password reset token generated and emailed for user {UserId}.", identityUser.Id);
            return Ok("If an account exists for this email, a password reset link has been sent.");
        }

        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] RequestResetPassword requestResetPassword)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Logger.LogInformation("Password reset submission received for {Email} from IP {Ip}.", requestResetPassword.Email, ip);
            var resetResult = await AccountService.ResetPassword(requestResetPassword, ip);
            if (resetResult.IsFailed)
            {
                Logger.LogWarning("Password reset failed for {Email} from IP {Ip}.", requestResetPassword.Email, ip);
                return ToActionResult(resetResult);
            }

            Logger.LogInformation("Password reset succeeded for {Email} from IP {Ip}.", requestResetPassword.Email, ip);
            return Ok("Password reset successfully!");
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();

            Logger.LogInformation("Refresh token rotation requested from IP {Ip}.", ip);
            var result = await AccountService.RefreshAsync(req.RefreshToken, ip, ua);
            if(result.IsFailed) {
                Logger.LogWarning("Refresh token rotation failed from IP {Ip}.", ip);
                return ToActionResult(result);
            }

            Logger.LogInformation("Refresh token rotation succeeded from IP {Ip}.", ip);
            return Ok(result.Value);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Logger.LogInformation("Logout requested from IP {Ip}.", ip);
            var refreshTokenHash = TokenHelpers.Sha256(req.RefreshToken);
            await RefreshTokenService.RevokeAllUserRefreshTokens(refreshTokenHash, ip);
            Logger.LogInformation("Logout completed from IP {Ip}.", ip);
            return NoContent();
        }

        private IActionResult ToActionResult(Result result)
        {
            if (!result.Errors[0].Metadata.TryGetValue("MethodName", out var methodName))
            {
                return BadRequest(result.Errors);
            }

            return methodName switch
            {
                "InternalServerError" => StatusCode(500, result.Errors),
                "BadRequest" => BadRequest(result.Errors),
                "Unauthorized" => Unauthorized(result.Errors),
                _ => BadRequest(result.Errors),
            };
        }

        private IActionResult ToActionResult<T>(Result<T> result)
        {
            if (!result.Errors[0].Metadata.TryGetValue("MethodName", out var methodName))
            {
                return BadRequest(result.Errors);
            }

            return methodName switch
            {
                "InternalServerError" => StatusCode(500, result.Errors),
                "BadRequest" => BadRequest(result.Errors),
                "Unauthorized" => Unauthorized(result.Errors),
                _ => BadRequest(result.Errors),
            };
        }
    }
}
