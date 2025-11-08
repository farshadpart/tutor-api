using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Data
{
    public class TutorContext : IdentityDbContext<User>
    {
        public TutorContext(DbContextOptions<TutorContext> options) : base(options) { }

        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Cycle> Cycles { get; set; }
    }
}
