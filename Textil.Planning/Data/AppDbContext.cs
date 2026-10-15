using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Textil.Planning.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Cycle> Cycles => Set<Cycle>();
    public DbSet<CycleMonth> CycleMonths => Set<CycleMonth>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<CycleSku> CycleSkus => Set<CycleSku>();
    public DbSet<Approval> Approvals => Set<Approval>();

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

        // Nothing in the planning model is ever deleted, so every relationship is Restrict.
        builder.Entity<Cycle>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Months).WithOne().HasForeignKey(m => m.CycleId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Batches).WithOne(b => b.Cycle).HasForeignKey(b => b.CycleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CycleMonth>(e =>
        {
            e.HasKey(m => new { m.CycleId, m.Month });
            e.Property(m => m.Month).HasMaxLength(7).IsFixedLength();
        });

        builder.Entity<Batch>(e =>
        {
            e.HasIndex(b => new { b.CycleId, b.Number }).IsUnique();
            e.Property(b => b.OriginalFileName).HasMaxLength(260).IsRequired();
            e.Property(b => b.OriginalFile).IsRequired();
            e.HasOne(b => b.UploadedBy).WithMany().HasForeignKey(b => b.UploadedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(b => b.Skus).WithOne().HasForeignKey(s => s.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(b => b.Approvals).WithOne().HasForeignKey(a => a.BatchId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CycleSku>(e =>
        {
            e.Property(s => s.Code).HasMaxLength(CycleSku.CodeMaxLength).IsRequired();
            e.HasIndex(s => new { s.CycleId, s.Code }).IsUnique();
            e.HasIndex(s => new { s.CycleId, s.Order });
            e.HasOne<Cycle>().WithMany().HasForeignKey(s => s.CycleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Approval>(e =>
        {
            e.HasIndex(a => new { a.BatchId, a.UserId }).IsUnique();
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Note).HasMaxLength(1000);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
