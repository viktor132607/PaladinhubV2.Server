using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Data;

/// <summary>
/// Access-control context mapped onto the existing ASP.NET Identity tables.
/// It does not introduce a second user/role system; it gives role-management
/// mutations one transaction boundary across Identity membership and granular
/// permission metadata.
/// </summary>
public sealed class AccessControlDbContext : DbContext
{
    public AccessControlDbContext(DbContextOptions<AccessControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<IdentityRole> Roles => Set<IdentityRole>();
    public DbSet<AccessControlUserRow> Users => Set<AccessControlUserRow>();
    public DbSet<AccessControlUserRoleRow> UserRoles => Set<AccessControlUserRoleRow>();
    public DbSet<RoleSecurityProfile> RoleSecurityProfiles => Set<RoleSecurityProfile>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleSecurityRevision> RoleSecurityRevisions => Set<RoleSecurityRevision>();
    public DbSet<AccessControlAuditEntry> AccessControlAuditEntries => Set<AccessControlAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        AccessControlModelConfiguration.Configure(builder, configureIdentityRole: true);

        builder.Entity<AccessControlUserRow>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).IsRequired();
            entity.Property(user => user.UserName).HasMaxLength(256);
            entity.Property(user => user.NormalizedUserName).HasMaxLength(256);
            entity.Property(user => user.Email).HasMaxLength(256);
        });

        builder.Entity<AccessControlUserRoleRow>(entity =>
        {
            entity.ToTable("AspNetUserRoles");
            entity.HasKey(row => new { row.UserId, row.RoleId });
            entity.Property(row => row.UserId).IsRequired();
            entity.Property(row => row.RoleId).IsRequired();
        });
    }
}
