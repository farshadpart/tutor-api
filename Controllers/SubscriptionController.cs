using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;
using Tutor.Api.Services;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService _subscriptionService;

        public SubscriptionController(SubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest createRequest)
        {
            await _subscriptionService.Create(createRequest);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("getSubscriptionGroups")]
        public IActionResult GetSubscriptionGroups()
        {
            return Ok(_subscriptionService.GetSubscriptionGroups());
        }
    }
}
