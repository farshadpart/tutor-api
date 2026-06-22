using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Data
{
    public class TutorContext(DbContextOptions<TutorContext> Options) : IdentityDbContext<User>(Options)
    {
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Cycle> Cycles => Set<Cycle>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

#if DEBUG
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.ConfigureWarnings(w =>
                w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
        }
#endif

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>()
                .HasIndex(x => x.TokenHash)
                .IsUnique();

            builder.Entity<RefreshToken>()
                .HasIndex(x => new { x.UserId, x.ExpiresAt });
        }
    }
}
