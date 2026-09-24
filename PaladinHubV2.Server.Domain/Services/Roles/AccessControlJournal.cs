using System.Text.Json;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

internal static class AccessControlJournal
{
    internal static void AddPermissions(
        AccessControlDbContext db,
        RoleSecurityProfile profile,
        IEnumerable<string> permissions,
        string actor)
    {
        foreach (string permission in permissions)
        {
            var entry = new RolePermission
            {
                RoleId = profile.RoleId,
                Profile = profile,
                PermissionId = permission,
                GrantedBy = actor,
                GrantedAtUtc = DateTime.UtcNow
            };

            profile.Permissions.Add(entry);
            db.RolePermissions.Add(entry);
        }
    }

    internal static void Bump(RoleSecurityProfile profile)
    {
        checked
        {
            profile.Version++;
        }

        profile.UpdatedAtUtc = DateTime.UtcNow;
    }

    internal static RoleSnapshot Snapshot(
        RoleSecurityProfile profile)
    {
        return new RoleSnapshot(
            profile.Role.Name ?? profile.RoleId,
            profile.IsDisabled,
            profile.Permissions
                .Select(permission => permission.PermissionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    internal static string SerializeSnapshot(
        RoleSecurityProfile profile) =>
        JsonSerializer.Serialize(Snapshot(profile));

    internal static void AddRevision(
        AccessControlDbContext db,
        RoleSecurityProfile profile,
        string action,
        string actor)
    {
        db.RoleSecurityRevisions.Add(new RoleSecurityRevision
        {
            RoleId = profile.RoleId,
            Profile = profile,
            Version = profile.Version,
            Action = action,
            Actor = actor,
            Snapshot = SerializeSnapshot(profile),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    internal static void AddAudit(
        AccessControlDbContext db,
        string action,
        AccessControlActor actor,
        string? roleId,
        string? roleName,
        string? userId,
        string? userName,
        string oldState,
        string newState)
    {
        db.AccessControlAuditEntries.Add(new AccessControlAuditEntry
        {
            Action = action,
            ActorId = actor.Id,
            Actor = actor.Name,
            TargetRoleId = roleId,
            TargetRoleName = roleName,
            TargetUserId = userId,
            TargetUserName = userName,
            OldState = oldState,
            NewState = newState,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    internal static RoleResponse ToResponse(
        RoleSecurityProfile profile,
        int userCount)
    {
        SystemRoleDefinition? system =
            SystemRoleCatalog.Find(profile.Role.Name);

        IReadOnlyList<string> permissions =
            system?.ProtectPermissionSet == true
                ? system.RequiredPermissions
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
                : profile.Permissions
                    .Select(permission => permission.PermissionId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

        return new RoleResponse(
            profile.RoleId,
            profile.Role.Name ?? profile.RoleId,
            profile.IsSystem || system is not null,
            profile.IsDisabled,
            profile.Version,
            userCount,
            permissions,
            profile.IsDeleted);
    }
}
