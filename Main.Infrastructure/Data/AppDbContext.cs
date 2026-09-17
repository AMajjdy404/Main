using Main.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Main.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasIndex(x => new { x.OwnerId, x.UserType });
            });
        }
    }
}
