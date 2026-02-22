using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;
using Tutor.Api.Services;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class SubscriptionController(SubscriptionService SubscriptionService) : ControllerBase
    {
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest createRequest)
        {
            await SubscriptionService.Create(createRequest);
            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("getSubscriptionGroups")]
        public IActionResult GetSubscriptionGroups()
        {
            return Ok(SubscriptionService.GetSubscriptionGroups());
        }
    }
}
