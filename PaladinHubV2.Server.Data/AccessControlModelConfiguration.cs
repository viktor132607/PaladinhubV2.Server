using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Data;

public static class AccessControlModelConfiguration
{
    public static void Configure(ModelBuilder builder, bool configureIdentityRole = false)
    {
        if (configureIdentityRole)
        {
            builder.Entity<IdentityRole>(entity =>
            {
                entity.ToTable("AspNetRoles");
                entity.HasKey(role => role.Id);
            });
        }

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
            entity.HasKey(permission => new { permission.RoleId, permission.PermissionId });
            entity.Property(permission => permission.RoleId).IsRequired();
            entity.Property(permission => permission.PermissionId).IsRequired().HasMaxLength(128);
            entity.Property(permission => permission.GrantedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(permission => permission.PermissionId);
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
            entity.Property(revision => revision.Action).IsRequired().HasMaxLength(30);
            entity.Property(revision => revision.Actor).IsRequired().HasMaxLength(256);
            entity.Property(revision => revision.Snapshot).IsRequired();
            entity.HasIndex(revision => new { revision.RoleId, revision.Version }).IsUnique();
            entity.HasOne(revision => revision.Profile)
                .WithMany(profile => profile.Revisions)
                .HasForeignKey(revision => revision.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccessControlAuditEntry>(entity =>
        {
            entity.ToTable("AccessControlAuditEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Action).IsRequired().HasMaxLength(40);
            entity.Property(entry => entry.ActorId).IsRequired().HasMaxLength(450);
            entity.Property(entry => entry.Actor).IsRequired().HasMaxLength(256);
            entity.Property(entry => entry.TargetRoleId).HasMaxLength(450);
            entity.Property(entry => entry.TargetRoleName).HasMaxLength(256);
            entity.Property(entry => entry.TargetUserId).HasMaxLength(450);
            entity.Property(entry => entry.TargetUserName).HasMaxLength(256);
            entity.Property(entry => entry.OldState).IsRequired();
            entity.Property(entry => entry.NewState).IsRequired();
            entity.HasIndex(entry => new { entry.TargetRoleId, entry.CreatedAtUtc });
            entity.HasIndex(entry => new { entry.TargetUserId, entry.CreatedAtUtc });
        });
    }
}
