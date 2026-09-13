using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Data;

/// <summary>
/// Companion context for role security metadata. Identity remains owned by
/// <see cref="AppDbContext"/>; this context maps the same AspNetRoles table only
/// so role-permission data can keep database-enforced foreign keys without
/// introducing a second user or role system.
/// </summary>
public sealed class AccessControlDbContext : DbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<RoleSecurityProfile> RoleSecurityProfiles => Set<RoleSecurityProfile>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleSecurityRevision> RoleSecurityRevisions => Set<RoleSecurityRevision>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("AspNetRoles");
            entity.HasKey(role => role.Id);
        });

        builder.Entity<RoleSecurityProfile>(entity =>
        {
            entity.ToTable("RoleSecurityProfiles");
            entity.HasKey(profile => profile.RoleId);
            entity.Property(profile => profile.RoleId).IsRequired();
            entity.Property(profile => profile.Version).IsConcurrencyToken();
            entity.HasOne(profile => profile.Role)
                .WithOne()
                .HasForeignKey<RoleSecurityProfile>(profile => profile.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(permission => new
            {
                permission.RoleId,
                permission.PermissionId
            });
            entity.Property(permission => permission.RoleId).IsRequired();
            entity.Property(permission => permission.PermissionId)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(permission => permission.GrantedBy)
                .IsRequired()
                .HasMaxLength(256);
            entity.HasOne(permission => permission.Profile)
                .WithMany(profile => profile.Permissions)
                .HasForeignKey(permission => permission.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoleSecurityRevision>(entity =>
        {
            entity.ToTable("RoleSecurityRevisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.RoleId).IsRequired();
            entity.Property(revision => revision.Action)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(revision => revision.Actor)
                .IsRequired()
                .HasMaxLength(256);
            entity.Property(revision => revision.Snapshot).IsRequired();
            entity.HasIndex(revision => new
            {
                revision.RoleId,
                revision.Version
            }).IsUnique();
            entity.HasOne(revision => revision.Profile)
                .WithMany(profile => profile.Revisions)
                .HasForeignKey(revision => revision.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
