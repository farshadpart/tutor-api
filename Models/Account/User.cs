using Microsoft.AspNetCore.Identity;
using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Models.Account
{
    public class User : IdentityUser
    {
        public List<Subscription> Subscriptions { get; set; } = [];
    }
}
