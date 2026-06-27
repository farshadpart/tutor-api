using System.Security.Claims;

namespace Tutor.Api.Utilities;

public static class IdentityHelper
{
    public static string GetUserIdentifier(this ClaimsPrincipal claimsPrincipal)
    {
        var userEmail = claimsPrincipal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(userEmail))
        {
            return userEmail;
        }
        
        var userName = claimsPrincipal.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrEmpty(userName))
        {
            return userName;
        }
        
        throw new Exception("User is not authenticated.");
    }
}