using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Roles;

public sealed class EffectivePermissionService
{
    private readonly string _connectionString;

    public EffectivePermissionService(AppDbContext database)
    {
        _connectionString = database.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Access-control database connection is unavailable.");
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<string>();
        }

        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var database = new AccessControlDbContext(options);

        var now = DateTimeOffset.UtcNow;
        if (!await database.Users.AsNoTracking().AnyAsync(user =>
                user.Id == userId && (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now),
                cancellationToken))
            return Array.Empty<string>();

        var memberships = await (
            from membership in database.UserRoles.AsNoTracking()
            join profile in database.RoleSecurityProfiles.AsNoTracking()
                on membership.RoleId equals profile.RoleId
            join role in database.Roles.AsNoTracking()
                on membership.RoleId equals role.Id
            where membership.UserId == userId && !profile.IsDisabled
            select new
            {
                profile.RoleId,
                profile.IsSystem,
                RoleName = role.Name ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        if (memberships.Length == 0)
        {
            return Array.Empty<string>();
        }

        var effective = new HashSet<string>(StringComparer.Ordinal);

        foreach (var membership in memberships.Where(value => value.IsSystem))
        {
            SystemRoleDefinition? systemRole = SystemRoleCatalog.Find(membership.RoleName);
            if (systemRole is null)
            {
                continue;
            }

            effective.UnionWith(systemRole.RequiredPermissions);
        }

        string[] activeRoleIds = memberships
            .Select(value => value.RoleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        string[] grants = await database.RolePermissions
            .AsNoTracking()
            .Where(grant => activeRoleIds.Contains(grant.RoleId))
            .Select(grant => grant.PermissionId)
            .ToArrayAsync(cancellationToken);

        foreach (string grant in grants.Where(AdminPermissions.IsKnown))
        {
            effective.Add(grant);

            PermissionDefinition definition = AdminPermissions.All.Single(value =>
                string.Equals(value.Id, grant, StringComparison.Ordinal));

            if (!string.Equals(
                    definition.Operation,
                    AdminPermissions.Operations.Manage,
                    StringComparison.Ordinal))
            {
                continue;
            }

            effective.UnionWith(
                AdminPermissions.ForResource(definition.Resource)
                    .Select(permission => permission.Id));
        }

        return effective
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        if (!AdminPermissions.IsKnown(permissionId))
        {
            return false;
        }

        IReadOnlyList<string> effective = await GetEffectivePermissionsAsync(
            userId,
            cancellationToken);

        return effective.Contains(permissionId, StringComparer.Ordinal);
    }
}
