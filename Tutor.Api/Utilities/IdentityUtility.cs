using Microsoft.AspNetCore.Identity;
using Tutor.Api.Data;
using Tutor.Api.Models.Account;

namespace Tutor.Api.Utilities;

public static class IdentityUtility
{
    public static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TutorContext>()
            .AddDefaultTokenProviders();
    }
}
