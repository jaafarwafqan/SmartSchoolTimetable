using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class LocalDbContext(DbContextOptions<LocalDbContext> options) : DbContext(options)
{
    public DbSet<OwnerAccount> Owners => Set<OwnerAccount>();
    public DbSet<LocalAuditEntry> AuditEntries => Set<LocalAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OwnerAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(account => account.Id);
            entity.HasIndex(account => account.OwnerSlot).IsUnique();
            entity.Property(account => account.OwnerSlot).HasMaxLength(16).IsRequired();
            entity.Property(account => account.Username).HasMaxLength(64).IsRequired();
            entity.Property(account => account.NormalizedUsername).HasMaxLength(64).IsRequired();
            entity.HasIndex(account => account.NormalizedUsername).IsUnique();
            entity.Property(account => account.PasswordSalt).IsRequired();
            entity.Property(account => account.PasswordHash).IsRequired();
            entity.Property(account => account.RecoverySalt).IsRequired();
            entity.Property(account => account.RecoveryCodeHash).IsRequired();
        });

        modelBuilder.Entity<LocalAuditEntry>(entity =>
        {
            entity.ToTable("AuditHistory");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.EventType).HasMaxLength(64).IsRequired();
            entity.Property(entry => entry.Target).HasMaxLength(128).IsRequired();
            entity.Property(entry => entry.Summary).HasMaxLength(512).IsRequired();
            entity.HasIndex(entry => entry.OccurredAt);
        });
    }
}
