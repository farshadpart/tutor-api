using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender<IdentityUser> _emailSender;
        private readonly ILogger<AccountController> _logger;

        public AccountController(UserManager<IdentityUser> userManager, IEmailSender<IdentityUser> emailSender, ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RequestCreateUser requestCreateUser)
        {
            var identityUser = new IdentityUser
            {
                UserName = requestCreateUser.Email,
                NormalizedUserName = requestCreateUser.Email.ToUpper(),
                Email = requestCreateUser.Email,
                NormalizedEmail = requestCreateUser.Email.ToUpper()
            };
            var identityResult = await _userManager.CreateAsync(identityUser, requestCreateUser.Password);

            if (!identityResult.Succeeded)
            {
                return BadRequest(identityResult.Errors);
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(identityUser);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
                throw new Exception("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
            }

            var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = identityUser.Id, token }, Request.Scheme);
            if (confirmationLink is null)
            {
                _logger.LogError("Failed to generate confirmation link for user {UserId}", identityUser.Id);
                return StatusCode(500, "Something went wrong!");
            }

#if !DEBUG
            await _emailSender.SendConfirmationLinkAsync(identityUser, identityUser.Email, confirmationLink);
#endif

            return Ok();
        }

        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var identityUser = await _userManager.FindByIdAsync(userId);
            if (identityUser is null)
            {
                _logger.LogError("Try to confirm invalid userId: {userId}", userId);
                return BadRequest("Something went wrong!");
            }
            var identityResult = await _userManager.ConfirmEmailAsync(identityUser, token);
            if (!identityResult.Succeeded)
            {
                _logger.LogError("Tried to confirm userId: {userId} with invalide code: {invalidCode}", userId, token);
                return BadRequest("Something went wrong!");
            }

            return Ok("Email confirmed successfully!");
        }
    }
}
