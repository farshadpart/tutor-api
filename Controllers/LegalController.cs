using Microsoft.AspNetCore.Mvc;
using Tutor.Api.Services.Legal;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LegalController : ControllerBase
    {
        [HttpGet("privacy")]
        public async Task<IActionResult> Privacy()
        {
            return Ok(PrivacyPolicy.VALUE);
        }
    }
}
