using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

internal static class AccessControlRules
{
    internal static string ValidateRoleName(string? value)
    {
        string name = value?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw Validation("Role name is required.");
        }

        if (name.Length > 256)
        {
            throw Validation("Role name cannot exceed 256 characters.");
        }

        if (name.Any(char.IsControl))
        {
            throw Validation(
                "Role name contains invalid control characters.");
        }

        return name;
    }

    internal static string[] ValidatePermissions(
        IEnumerable<string>? values)
    {
        string[] permissions = (values ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        string[] unknown = permissions
            .Where(permission => !AdminPermissions.IsKnown(permission))
            .ToArray();

        if (unknown.Length != 0)
        {
            throw Validation(
                $"Unknown permission(s): {string.Join(", ", unknown)}.");
        }

        return permissions;
    }

    internal static string NormalizeName(string name) =>
        name.ToUpperInvariant();

    internal static async Task<RoleSecurityProfile> RequireProfileAsync(
        AccessControlDbContext db,
        string roleId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            throw Validation("Role ID is required.");
        }

        IQueryable<RoleSecurityProfile> query = db.RoleSecurityProfiles
            .Include(profile => profile.Role)
            .Include(profile => profile.Permissions)
            .Include(profile => profile.Revisions);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
                profile => profile.RoleId == roleId,
                cancellationToken)
            ?? throw NotFound("Role was not found.");
    }

    internal static async Task<AccessControlUserRow> RequireUserAsync(
        AccessControlDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw Validation("User ID is required.");
        }

        return await db.Users.SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken)
            ?? throw NotFound("User was not found.");
    }

    internal static void RequireVersion(
        RoleSecurityProfile profile,
        int expectedVersion)
    {
        if (expectedVersion <= 0)
        {
            throw Validation("A positive role version is required.");
        }

        if (profile.Version != expectedVersion)
        {
            throw Conflict(
                $"Role was changed by another request. Current version is {profile.Version}.");
        }
    }

    internal static AccessControlAdminException Validation(string message) =>
        new(AccessControlFailure.Validation, message);

    internal static AccessControlAdminException NotFound(string message) =>
        new(AccessControlFailure.NotFound, message);

    internal static AccessControlAdminException Conflict(string message) =>
        new(AccessControlFailure.Conflict, message);
}
