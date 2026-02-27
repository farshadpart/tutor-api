using FluentResults;
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
    public class AccountController(AccountService AccountService, RefreshTokenService RefreshTokenService, ILogger<AccountController> Logger) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RequestLogin requestLogin)
        {
            var userResult = await AccountService.ValidateLoginRequest(requestLogin);
            if (userResult.IsFailed)
            {
                return ToActionResult(userResult);
            }

            var accessToken = await AccountService.CreateAccessToken(userResult.Value);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();
            var refreshToken = await RefreshTokenService.CreateRefreshToken(userResult.Value, ip, ua);

            return Ok(new TokenHolder(accessToken, refreshToken));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RequestCreateUser requestCreateUser)
        {
            var registerResult = await AccountService.Register(requestCreateUser);
            if (registerResult.IsFailed)
            {
                return ToActionResult(registerResult);
            }

            var (identityUser, token) = registerResult.Value;
            var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = identityUser.Id, token }, Request.Scheme);
            if (confirmationLink is null)
            {
                Logger.LogError("Failed to generate confirmation link for user {UserId}", identityUser.Id);
                return StatusCode(500, "Something went wrong!");
            }

#if !DEBUG
            await _emailSender.SendConfirmationLinkAsync(identityUser, identityUser.Email, confirmationLink);
#endif

            return Ok("User registered successfully! Please check your email to confirm your account.");
        }

        [HttpGet("confirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var confirmResult = await AccountService.ConfirmEmail(userId, token);
            if (confirmResult.IsFailed)
            {
                return ToActionResult(confirmResult);
            }

            return Ok("Email confirmed successfully!");
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = Request.Headers.UserAgent.ToString();

            var result = await AccountService.RefreshAsync(req.RefreshToken, ip, ua);
            if(result.IsFailed) {
                return ToActionResult(result);
            }

            return Ok(result.Value);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshTokenHash = TokenHelpers.Sha256(req.RefreshToken);
            await RefreshTokenService.RevokeAllUserRefreshTokens(refreshTokenHash, ip);
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
                "BadReqeust" => BadRequest(result.Errors),
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
                "BadReqeust" => BadRequest(result.Errors),
                "Unauthorized" => Unauthorized(result.Errors),
                _ => BadRequest(result.Errors),
            };
        }
    }
}
