using Microsoft.EntityFrameworkCore;

namespace Ladeco.Diag.Infrastructure.Persistence;

public sealed class LadecoDiagDbContext : DbContext
{
    public LadecoDiagDbContext(DbContextOptions<LadecoDiagDbContext> options) : base(options)
    {
    }

    public DbSet<ScanHistoryEntry> ScanHistory => Set<ScanHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScanHistoryEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ComputerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UserName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ReportJson).IsRequired();
            entity.Property(x => x.ScannedAt)
                .HasConversion(
                    v => v.ToUnixTimeMilliseconds(),
                    v => DateTimeOffset.FromUnixTimeMilliseconds(v))
                .IsRequired();
            entity.HasIndex(x => new { x.CustomerName, x.ComputerName, x.ScannedAt });
        });
    }
}
