using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SensorX.Gateway.Domain.Entities;
using System.Text.Json;

namespace SensorX.Gateway.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ──
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Email).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.IsLocked).HasDefaultValue(false);
            e.Property(u => u.LoginFailCount).HasDefaultValue(0);
            e.Property(u => u.LockCount).HasDefaultValue(0);

            e.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // ── Roles ──
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.Name).IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
        });

        // ── UserRoles ──
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
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
            e.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rt => rt.UserId).HasFilter("\"IsRevoked\" = false")
                .HasDatabaseName("idx_rt_user_active");
        });
    }
}