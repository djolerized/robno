using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Textil.Planning.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.HasOne(u => u.Manager)
                .WithMany()
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Entity).HasMaxLength(50).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(64).IsRequired();
            e.Property(a => a.Action).HasMaxLength(50).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.Entity, a.EntityId });
            e.HasIndex(a => a.At);
        });
    }
}
