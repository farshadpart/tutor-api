using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UserSettingsController(
        UserSettingsService userSettingsService,
        ILogger<UserSettingsController> logger) : ControllerBase
    {
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                return Unauthorized();
            }

            logger.LogDebug("Getting settings for user {UserId}.", userId);
            var userSettings = await userSettingsService.Get(userId);
            logger.LogInformation("Settings retrieved for user {UserId}.", userId);

            return Ok(userSettings);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] RequestUpdateUserSettings requestUpdateUserSettings)
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                return Unauthorized();
            }

            logger.LogDebug("Updating settings for user {UserId}.", userId);
            await userSettingsService.Update(userId, requestUpdateUserSettings);
            logger.LogInformation("Settings updated for user {UserId}.", userId);

            return Ok();
        }

        [HttpGet("getUserAvatar")]
        public async Task<IActionResult> GetUserAvatar()
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                logger.LogWarning("Avatar request rejected because the user ID claim is missing.");
                return Unauthorized();
            }

            var avatarFile = await userSettingsService.GetUserAvatarFile(userId);
            if (avatarFile is null)
            {
                return NotFound();
            }

            return PhysicalFile(avatarFile.FilePath, avatarFile.ContentType);
        }

        [HttpPut("updateUserAvatar")]
        public async Task<IActionResult> UpdateUserAvatar([FromForm] IFormFile image)
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                return Unauthorized();
            }

            try
            {
                await userSettingsService.UpdateUserAvatar(userId, image);
                return Ok();
            }
            catch (ArgumentException ex)
            {
                return BadRequest();
            }
        }
    }
}
