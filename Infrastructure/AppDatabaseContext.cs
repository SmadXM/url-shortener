using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<ShortLink> ShortLinks => Set<ShortLink>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<User>(entity =>
            {
                entity.HasKey(x => x.GlobalId);
                entity.HasIndex(x => x.Email).IsUnique();
                entity.Property(x => x.Email).HasMaxLength(256);
            });

            builder.Entity<ShortLink>(entity =>
            {
                entity.HasKey(x => x.GlobalId);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.OriginalUrl).HasMaxLength(2048);
                entity.Property(x => x.Code).HasMaxLength(10);
                entity.HasIndex(x => x.OriginalUrl);
                entity.HasOne<User>()
                    .WithMany(u => u.ShortLinks)
                    .HasForeignKey(x => x.UserGlobalId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
