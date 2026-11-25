using LoyaltyPoints.Core;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyPoints.Api.Data;

public class LoyaltyDbContext : DbContext
{
    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.CpfHash).IsUnique();
            e.Property(a => a.CpfHash).HasMaxLength(64).IsRequired();
            e.Property(a => a.CpfMasked).HasMaxLength(20).IsRequired();
            e.HasMany(a => a.Ledger).WithOne().HasForeignKey(l => l.AccountId);
        });

        modelBuilder.Entity<LedgerEntry>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Amount).HasPrecision(18, 2);
        });
    }
}
