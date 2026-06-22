using System.Security.Claims;

namespace Tutor.Api.Services
{
    public static class ExtensionMethods
    {
        public static string? GetClaimValue(this ClaimsPrincipal claimsPrincipal, string claimType)
        {
            return claimsPrincipal.FindFirstValue(claimType);
        }
    }
}
