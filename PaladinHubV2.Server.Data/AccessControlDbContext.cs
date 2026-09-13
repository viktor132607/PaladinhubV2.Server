using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Data;

/// <summary>
/// Companion context used by focused access-control model/integration tests.
/// Runtime mutations also map the same tables through AppDbContext so Identity
/// role changes and permission metadata can commit atomically.
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
    public DbSet<AccessControlAuditEntry> AccessControlAuditEntries => Set<AccessControlAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        AccessControlModelConfiguration.Configure(builder, configureIdentityRole: true);
    }
}
