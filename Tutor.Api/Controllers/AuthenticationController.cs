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
    public class AuthenticationController(
        AuthenticationService authenticationService,
        AccountService accountService) : Controller
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RequestLogin requestLogin)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();
            
            var result = await authenticationService.Login(requestLogin, ip, ua);
            return result.IsFailed 
                ? result.ToHttpError(this) 
                : Ok(result.Value);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RequestCreateUser requestCreateUser)
        {
            var confirmationEndpoint = Url.ActionLink(nameof(ConfirmEmail));
            if (confirmationEndpoint is null) 
                return StatusCode(500, "Something went wrong!");

            var result = await accountService.RegisterAndSendConfirmation(requestCreateUser, confirmationEndpoint);
            return result.IsFailed
                ? result.ToHttpError(this)
                : Ok("User registered successfully! Please check your email to confirm your account.");
        }

        [HttpGet("confirmEmail")]
        [Produces("text/html")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string? userId, [FromQuery] string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return EmailConfirmationView(
                    "Email link is incomplete",
                    "We could not confirm this account because the confirmation link is missing required information.",
                    "Please use the latest confirmation email from Tutor. If the problem continues, request a new confirmation email.",
                    false,
                    StatusCodes.Status400BadRequest);
            }

            var confirmResult = await accountService.ConfirmEmail(userId, token);
            if (confirmResult.IsFailed)
            {
                return EmailConfirmationView(
                    "Email link expired or invalid",
                    "We could not confirm this account with the link provided.",
                    "For your security, confirmation links can only be used once and may expire. Please sign in or register again to receive a fresh confirmation email.",
                    false,
                    StatusCodes.Status400BadRequest);
            }
            
            return EmailConfirmationView(
                "Email confirmed",
                "Your Tutor account is ready to use.",
                "You can now return to Tutor and sign in with your email and password.",
                true,
                StatusCodes.Status200OK);
        }

        [HttpPost("forgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] RequestForgotPassword requestForgotPassword)
        {
            var result = await accountService.SendPasswordResetEmail(requestForgotPassword);
            return result.IsFailed
                ? result.ToHttpError(this)
                : Ok("If an account exists for this email, a password reset link has been sent.");
        }

        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] RequestResetPassword requestResetPassword)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var resetResult = await accountService.ResetPassword(requestResetPassword, ip);
            return resetResult.IsFailed
                ? resetResult.ToHttpError(this)
                : Ok("Password reset successfully!");
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();

            var result = await authenticationService.RefreshAsync(req.RefreshToken, ip, ua);
            return result.IsFailed ? result.ToHttpError(this) : Ok(result.Value);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await authenticationService.Logout(req.RefreshToken, ip);
            return NoContent();
        }

        private IActionResult EmailConfirmationView(string title, string message, string guidance, bool isSuccess, int statusCode)
        {
            Response.StatusCode = statusCode;
            return View("ConfirmEmail", new EmailConfirmationViewModel(title, message, guidance, isSuccess));
        }
    }
}
