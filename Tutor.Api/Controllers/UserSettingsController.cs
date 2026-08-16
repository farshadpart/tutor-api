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
    }
}
