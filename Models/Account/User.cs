using Microsoft.AspNetCore.Identity;
using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Models.Account
{
    public class User : IdentityUser, IBaseEntity<string>
    {
        public List<Subscription> Subscriptions { get; set; } = [];
        public DateTimeOffset CreatedAt { get; set; }
        public uint RowVersion { get; set; }
    }
}
