using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;
using Tutor.Api.Services;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class SubscriptionController(ISubscriptionService SubscriptionService, ILogger<SubscriptionController> Logger) : ControllerBase
    {
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest createRequest)
        {
            Logger.LogInformation(
                "Subscription create requested for user {UserId} with group {SubscriptionGroup}.",
                createRequest.UserId,
                createRequest.SubscriptionGroup);

            await SubscriptionService.Create(createRequest);

            Logger.LogInformation(
                "Subscription create completed for user {UserId} with group {SubscriptionGroup}.",
                createRequest.UserId,
                createRequest.SubscriptionGroup);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("getSubscriptionGroups")]
        public IActionResult GetSubscriptionGroups()
        {
            Logger.LogDebug("Subscription groups requested.");
            var subscriptionGroups = SubscriptionService.GetSubscriptionGroups();
            Logger.LogInformation("Returned {SubscriptionGroupCount} subscription groups.", subscriptionGroups.Count);
            return Ok(subscriptionGroups);
        }
    }
}
