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

        [HttpPost("active")]
        public async Task<IActionResult> Active([FromBody] ActiveSubscriptionRequest activeSubscriptionRequest)
        {
            await _subscriptionService.Active(activeSubscriptionRequest);
            return Ok();
        }

        [HttpGet("getSubscriptionTypes")]
        public async Task<IActionResult> GetSubscriptionTypes()
        {
            return Ok(await _subscriptionService.GetSubscriptionTypes());
        }
    }
}
