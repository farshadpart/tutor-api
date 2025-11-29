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
using Tutor.Api.Services;
using Tutor.Api.Services.Legal;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LegalController : ControllerBase
    {
        private readonly ILogger<LegalController> _logger;

        public LegalController(ILogger<LegalController> logger)
        {
            _logger = logger;
        }

        [HttpGet("privacy")]
        public async Task<IActionResult> Privacy()
        {
            return Ok(PrivacyPolicy.VALUE);
        }
    }
}
