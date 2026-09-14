using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

public sealed class AccessControlMutationGuard
{
    private readonly DbContextOptions<AccessControlDbContext> _options;

    public AccessControlMutationGuard(DbContextOptions<AccessControlDbContext> options)
    {
        _options = options;
    }

    public static AccessControlMutationGuard ForPostgres(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AccessControlMutationGuard(options);
    }

    public Task EnsureAssignmentIsSafeAsync(
        string roleId,
        string userId,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        _ = roleId;
        _ = cancellationToken;
        if (string.Equals(actor.Id, userId, StringComparison.Ordinal))
        {
            throw Conflict("Users cannot assign roles to themselves.");
        }

        return Task.CompletedTask;
    }

    public async Task EnsureRoleUpdateIsSafeAsync(
        string roleId, UpdateRoleRequest request, AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await EnsureRoleUpdateIsSafeAsync(db, roleId, request, actor, cancellationToken);
    }

    internal static async Task EnsureRoleUpdateIsSafeAsync(
        AccessControlDbContext db, string roleId, UpdateRoleRequest request,
        AccessControlActor actor, CancellationToken cancellationToken)
    {
        if (!await IsActorAssignedAsync(db, roleId, actor.Id, cancellationToken))
        {
            return;
        }

        RoleSecurityProfile? profile = await db.RoleSecurityProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RoleId == roleId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        if (profile.IsDisabled && !request.IsDisabled)
        {
            throw Conflict("Users cannot re-enable a role assigned to themselves.");
        }
    }

    public async Task EnsurePermissionReplacementIsSafeAsync(
        string roleId, ReplaceRolePermissionsRequest request, AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await EnsurePermissionReplacementIsSafeAsync(db, roleId, request, actor, cancellationToken);
    }

    internal static async Task EnsurePermissionReplacementIsSafeAsync(
        AccessControlDbContext db, string roleId, ReplaceRolePermissionsRequest request,
        AccessControlActor actor, CancellationToken cancellationToken)
    {
        if (!await IsActorAssignedAsync(db, roleId, actor.Id, cancellationToken))
        {
            return;
        }

        string[] current = await db.RolePermissions
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .Select(item => item.PermissionId)
            .ToArrayAsync(cancellationToken);

        string[] requested = (request.Permissions ?? Array.Empty<string>())
            .Where(AdminPermissions.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        string[] added = requested
            .Except(current, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (added.Length != 0)
        {
            throw Conflict(
                "Users cannot grant new permissions to a role assigned to themselves.");
        }
    }

    public async Task EnsureRestoreIsSafeAsync(
        string roleId, RestoreRoleRevisionRequest request, AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await EnsureRestoreIsSafeAsync(db, roleId, request, actor, cancellationToken);
    }

    internal static async Task EnsureRestoreIsSafeAsync(
        AccessControlDbContext db, string roleId, RestoreRoleRevisionRequest request,
        AccessControlActor actor, CancellationToken cancellationToken)
    {
        if (!await IsActorAssignedAsync(db, roleId, actor.Id, cancellationToken))
        {
            return;
        }

        RoleSecurityProfile? profile = await db.RoleSecurityProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RoleId == roleId, cancellationToken);
        RoleSecurityRevision? revision = await db.RoleSecurityRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.RoleId == roleId && item.Version == request.RevisionVersion,
                cancellationToken);
        if (profile is null || revision is null)
        {
            return;
        }

        RoleSnapshot? restored;
        try
        {
            restored = JsonSerializer.Deserialize<RoleSnapshot>(revision.Snapshot);
        }
        catch (JsonException)
        {
            return;
        }

        if (restored is null)
        {
            return;
        }

        if (profile.IsDisabled && !restored.IsDisabled)
        {
            throw Conflict("Users cannot restore a role assigned to themselves into an enabled state.");
        }

        string[] current = await db.RolePermissions
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .Select(item => item.PermissionId)
            .ToArrayAsync(cancellationToken);
        string[] added = restored.Permissions
            .Where(AdminPermissions.IsKnown)
            .Except(current, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (added.Length != 0)
        {
            throw Conflict(
                "Users cannot restore additional permissions onto a role assigned to themselves.");
        }
    }

    private static Task<bool> IsActorAssignedAsync(
        AccessControlDbContext db,
        string roleId,
        string actorId,
        CancellationToken cancellationToken)
    {
        return db.UserRoles
            .AsNoTracking()
            .AnyAsync(
                item => item.RoleId == roleId && item.UserId == actorId,
                cancellationToken);
    }

    private static AccessControlAdminException Conflict(string message) =>
        new(AccessControlFailure.Conflict, message);
}
