using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tutor.Api.Data
{
    public class TutorContext : IdentityDbContext<IdentityUser>
    {
        public TutorContext(DbContextOptions<TutorContext> options) : base(options)
        {
            
        }
    }
}
