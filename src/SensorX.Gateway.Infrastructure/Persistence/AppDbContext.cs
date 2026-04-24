using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SensorX.Gateway.Domain.Entities;
using System.Text.Json;

namespace SensorX.Gateway.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Accounts ──
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Email).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).IsRequired();
            e.Property(u => u.AvatarUrl).IsRequired(false);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.IsLocked).HasDefaultValue(false);
            e.Property(u => u.LoginFailCount).HasDefaultValue(0);
            e.Property(u => u.LockCount).HasDefaultValue(0);

            e.Property(u => u.Role).IsRequired().HasConversion<int>();

            e.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // ── RefreshTokens ──
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(rt => rt.TokenHmac).IsRequired();
            e.HasIndex(rt => rt.TokenHmac).IsUnique();
            e.Property(rt => rt.IsRevoked).HasDefaultValue(false);
            e.Property(rt => rt.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(rt => rt.ExpiresAt).IsRequired();
            e.HasOne(rt => rt.Account).WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rt => rt.UserId).HasFilter("\"IsRevoked\" = false")
                .HasDatabaseName("idx_rt_user_active");
        });
    }
}