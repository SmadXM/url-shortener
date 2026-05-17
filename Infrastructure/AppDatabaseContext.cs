using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<ShortLink>(entity =>
            {
                entity.HasKey(x => x.GlobalId);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.OriginalUrl).HasMaxLength(2048);
                entity.Property(x => x.Code).HasMaxLength(10);
                entity.HasIndex(x => x.OriginalUrl);
            });
        }
    }
}
