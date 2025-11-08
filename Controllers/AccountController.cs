using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender<User> _emailSender;
        private readonly ILogger<AccountController> _logger;
        private readonly AppSettings _appSettings;

        public AccountController(UserManager<User> userManager, IEmailSender<User> emailSender, ILogger<AccountController> logger, AppSettings appSettings)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _appSettings = appSettings;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] RequestLogin requestLogin)
        {
            var identityUser = await _userManager.FindByEmailAsync(requestLogin.Email);
            
            if (identityUser is null)
            {
                return Unauthorized("Invalid email or password!");
            }
            var passwordValid = await _userManager.CheckPasswordAsync(identityUser, requestLogin.Password);
            if (!passwordValid)
            {
                return Unauthorized("Invalid email or password!");
            }
            if (!await _userManager.IsEmailConfirmedAsync(identityUser))
            {
                return Unauthorized("Email not confirmed!");
            }

            var userRoles = await _userManager.GetRolesAsync(identityUser);
            var userClaims = await _userManager.GetClaimsAsync(identityUser);

            var singingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.Jwt.SecretKey));
            var credentials = new SigningCredentials(singingKey, SecurityAlgorithms.HmacSha256);
            List<Claim> claims = [];

            claims.Add(new Claim(ClaimTypes.Email, requestLogin.Email));
            claims.Add(new Claim(TutorClaimTypes.Id, identityUser.Id));
            claims.AddRange(userClaims);
            foreach (var role in userRoles) 
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_appSettings.Jwt.ExpirationMinutes),
                SigningCredentials = credentials,
                Issuer = _appSettings.Jwt.Issuer,
                Audience = _appSettings.Jwt.Audience,
                IssuedAt = DateTime.UtcNow
            };
            
            return Ok(new { accessToken = new JsonWebTokenHandler().CreateToken(tokenDescriptor) });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RequestCreateUser requestCreateUser)
        {
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
