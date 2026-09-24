using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

public sealed class AccessControlAdminService
{
    private const long MutationAdvisoryLock = 8820416;

    private readonly DbContextOptions<AccessControlDbContext> _options;
    private readonly AccessControlQueryService _queries;

    public AccessControlAdminService(
        DbContextOptions<AccessControlDbContext> options)
    {
        _options = options;
        _queries = new AccessControlQueryService(options);
    }

    public static AccessControlAdminService ForPostgres(
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new AccessControlAdminService(options);
    }

    public Task<IReadOnlyList<RoleResponse>> ListRolesAsync(
        CancellationToken cancellationToken = default) =>
        _queries.ListRolesAsync(cancellationToken);

    public Task<RoleResponse> GetRoleAsync(
        string roleId,
        CancellationToken cancellationToken = default) =>
        _queries.GetRoleAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<PermissionDefinition>>
        GetPermissionCatalogAsync() =>
        _queries.GetPermissionCatalogAsync();

    public Task<IReadOnlyList<RoleUserSummary>> GetRoleUsersAsync(
        string roleId,
        CancellationToken cancellationToken = default) =>
        _queries.GetRoleUsersAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<UserRoleSummary>> ListUsersAsync(
        string? search,
        CancellationToken cancellationToken = default) =>
        _queries.ListUsersAsync(search, cancellationToken);

    public Task<IReadOnlyList<RoleSecurityRevisionResponse>>
        GetHistoryAsync(
            string roleId,
            CancellationToken cancellationToken = default) =>
        _queries.GetHistoryAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<AccessControlAuditResponse>>
        GetAuditAsync(
            string? roleId,
            string? userId,
            CancellationToken cancellationToken = default) =>
        _queries.GetAuditAsync(
            roleId,
            userId,
            cancellationToken);

    public async Task<RoleResponse> CreateRoleAsync(
        CreateRoleRequest request,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name =
            AccessControlRules.ValidateRoleName(request.Name);

        if (SystemRoleCatalog.IsSystemRole(name))
        {
            throw AccessControlRules.Conflict(
                "System role names are reserved.");
        }

        string[] permissions =
            AccessControlRules.ValidatePermissions(
                request.Permissions ?? Array.Empty<string>());

        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        string normalizedName =
            AccessControlRules.NormalizeName(name);

        if (await db.Roles.AnyAsync(
                role => role.NormalizedName == normalizedName,
                cancellationToken))
        {
            throw AccessControlRules.Conflict(
                "A role with this name already exists.");
        }

        var role = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            NormalizedName = normalizedName,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var profile = new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role,
            IsSystem = false,
            IsDisabled = false,
            Version = 1,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Roles.Add(role);
        db.RoleSecurityProfiles.Add(profile);

        AccessControlJournal.AddPermissions(
            db,
            profile,
            permissions,
            actor.Name);

        AccessControlJournal.AddRevision(
            db,
            profile,
            "create",
            actor.Name);

        AccessControlJournal.AddAudit(
            db,
            "role.create",
            actor,
            role.Id,
            name,
            null,
            null,
            "{}",
            AccessControlJournal.SerializeSnapshot(profile));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);

        return AccessControlJournal.ToResponse(profile, 0);
    }

    public async Task<RoleResponse> UpdateRoleAsync(
        string roleId,
        UpdateRoleRequest request,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name =
            AccessControlRules.ValidateRoleName(request.Name);

        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        await AccessControlMutationGuard
            .EnsureRoleUpdateIsSafeAsync(
                db,
                roleId,
                request,
                actor,
                cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        if (profile.IsDeleted)
        {
            throw AccessControlRules.Conflict(
                "Restore the deleted role before changing it.");
        }

        AccessControlRules.RequireVersion(
            profile,
            request.Version);

        RoleSnapshot before =
            AccessControlJournal.Snapshot(profile);

        SystemRoleDefinition? systemRole =
            SystemRoleCatalog.Find(profile.Role.Name);

        if (systemRole is not null)
        {
            if (systemRole.ProtectName &&
                !string.Equals(
                    name,
                    systemRole.Name,
                    StringComparison.Ordinal))
            {
                throw AccessControlRules.Conflict(
                    "System role name cannot be changed.");
            }

            if (request.IsDisabled)
            {
                throw AccessControlRules.Conflict(
                    "System administrator role cannot be disabled.");
            }
        }
        else if (SystemRoleCatalog.IsSystemRole(name))
        {
            throw AccessControlRules.Conflict(
                "System role names are reserved.");
        }

        string normalizedName =
            AccessControlRules.NormalizeName(name);

        if (await db.Roles.AnyAsync(
                role =>
                    role.Id != roleId &&
                    role.NormalizedName == normalizedName,
                cancellationToken))
        {
            throw AccessControlRules.Conflict(
                "A role with this name already exists.");
        }

        profile.Role.Name = name;
        profile.Role.NormalizedName = normalizedName;
        profile.Role.ConcurrencyStamp =
            Guid.NewGuid().ToString();
        profile.IsDisabled = request.IsDisabled;

        AccessControlJournal.Bump(profile);
        AccessControlJournal.AddRevision(
            db,
            profile,
            "update",
            actor.Name);

        AccessControlJournal.AddAudit(
            db,
            "role.update",
            actor,
            roleId,
            name,
            null,
            null,
            JsonSerializer.Serialize(before),
            AccessControlJournal.SerializeSnapshot(profile));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);

        int count = await db.UserRoles.CountAsync(
            row => row.RoleId == roleId,
            cancellationToken);

        return AccessControlJournal.ToResponse(
            profile,
            count);
    }

    public async Task<RoleResponse> ReplacePermissionsAsync(
        string roleId,
        ReplaceRolePermissionsRequest request,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string[] requested =
            AccessControlRules.ValidatePermissions(
                request.Permissions);

        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        await AccessControlMutationGuard
            .EnsurePermissionReplacementIsSafeAsync(
                db,
                roleId,
                request,
                actor,
                cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        if (profile.IsDeleted)
        {
            throw AccessControlRules.Conflict(
                "Restore the deleted role before changing it.");
        }

        AccessControlRules.RequireVersion(
            profile,
            request.Version);

        RoleSnapshot before =
            AccessControlJournal.Snapshot(profile);

        SystemRoleDefinition? systemRole =
            SystemRoleCatalog.Find(profile.Role.Name);

        if (systemRole?.ProtectPermissionSet == true &&
            !requested
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(systemRole.RequiredPermissions))
        {
            throw AccessControlRules.Conflict(
                "The system administrator role must keep the complete permission set.");
        }

        string[] current = profile.Permissions
            .Select(permission => permission.PermissionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (current.SequenceEqual(
                requested,
                StringComparer.Ordinal))
        {
            int currentCount =
                await db.UserRoles.CountAsync(
                    row => row.RoleId == roleId,
                    cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return AccessControlJournal.ToResponse(
                profile,
                currentCount);
        }

        db.RolePermissions.RemoveRange(
            profile.Permissions);

        profile.Permissions.Clear();

        AccessControlJournal.AddPermissions(
            db,
            profile,
            requested,
            actor.Name);

        AccessControlJournal.Bump(profile);
        AccessControlJournal.AddRevision(
            db,
            profile,
            "permissions",
            actor.Name);

        AccessControlJournal.AddAudit(
            db,
            "role.permissions",
            actor,
            roleId,
            profile.Role.Name,
            null,
            null,
            JsonSerializer.Serialize(before),
            AccessControlJournal.SerializeSnapshot(profile));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);

        int count = await db.UserRoles.CountAsync(
            row => row.RoleId == roleId,
            cancellationToken);

        return AccessControlJournal.ToResponse(
            profile,
            count);
    }

    public async Task<RoleResponse> RestoreRevisionAsync(
        string roleId,
        RestoreRoleRevisionRequest request,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        await AccessControlMutationGuard
            .EnsureRestoreIsSafeAsync(
                db,
                roleId,
                request,
                actor,
                cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        AccessControlRules.RequireVersion(
            profile,
            request.Version);

        RoleSecurityRevision revision =
            await db.RoleSecurityRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.RoleId == roleId &&
                        item.Version == request.RevisionVersion,
                    cancellationToken)
            ?? throw AccessControlRules.NotFound(
                "Role revision was not found.");

        if (revision.Action == "delete")
        {
            throw AccessControlRules.Conflict(
                "Choose a revision from before deletion.");
        }

        RoleSnapshot restored =
            JsonSerializer.Deserialize<RoleSnapshot>(
                revision.Snapshot)
            ?? throw AccessControlRules.Conflict(
                "Role revision snapshot is invalid.");

        AccessControlRules.ValidateRoleName(
            restored.Name);

        string[] permissions =
            AccessControlRules.ValidatePermissions(
                restored.Permissions);

        RoleSnapshot before =
            AccessControlJournal.Snapshot(profile);

        SystemRoleDefinition? systemRole =
            SystemRoleCatalog.Find(profile.Role.Name);

        if (systemRole is not null)
        {
            if (!string.Equals(
                    restored.Name,
                    systemRole.Name,
                    StringComparison.Ordinal) ||
                restored.IsDisabled ||
                !permissions
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(systemRole.RequiredPermissions))
            {
                throw AccessControlRules.Conflict(
                    "Revision would violate protected system-role invariants.");
            }
        }
        else
        {
            if (SystemRoleCatalog.IsSystemRole(
                    restored.Name))
            {
                throw AccessControlRules.Conflict(
                    "Revision would use a reserved system-role name.");
            }

            string normalized =
                AccessControlRules.NormalizeName(
                    restored.Name);

            if (await db.Roles.AnyAsync(
                    role =>
                        role.Id != roleId &&
                        role.NormalizedName == normalized,
                    cancellationToken))
            {
                throw AccessControlRules.Conflict(
                    "Revision would conflict with an existing role name.");
            }
        }

        profile.Role.Name = restored.Name;
        profile.Role.NormalizedName =
            AccessControlRules.NormalizeName(
                restored.Name);
        profile.Role.ConcurrencyStamp =
            Guid.NewGuid().ToString();
        profile.IsDisabled = restored.IsDisabled;

        db.RolePermissions.RemoveRange(
            profile.Permissions);
        profile.Permissions.Clear();

        AccessControlJournal.AddPermissions(
            db,
            profile,
            permissions,
            actor.Name);

        AccessControlJournal.Bump(profile);
        AccessControlJournal.AddRevision(
            db,
            profile,
            "restore",
            actor.Name);

        AccessControlJournal.AddAudit(
            db,
            "role.restore",
            actor,
            roleId,
            profile.Role.Name,
            null,
            null,
            JsonSerializer.Serialize(before),
            AccessControlJournal.SerializeSnapshot(profile));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);

        int count = await db.UserRoles.CountAsync(
            row => row.RoleId == roleId,
            cancellationToken);

        return AccessControlJournal.ToResponse(
            profile,
            count);
    }

    public async Task DeleteRoleAsync(
        string roleId,
        int version,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        if (profile.IsDeleted)
        {
            throw AccessControlRules.Conflict(
                "Restore the deleted role before changing it.");
        }

        AccessControlRules.RequireVersion(
            profile,
            version);

        if (profile.IsSystem ||
            SystemRoleCatalog.IsSystemRole(
                profile.Role.Name))
        {
            throw AccessControlRules.Conflict(
                "System roles cannot be deleted.");
        }

        int users = await db.UserRoles.CountAsync(
            row => row.RoleId == roleId,
            cancellationToken);

        if (users != 0)
        {
            throw AccessControlRules.Conflict(
                "Role is still assigned to users and cannot be deleted.");
        }

        string oldState =
            AccessControlJournal.SerializeSnapshot(
                profile);

        AccessControlJournal.AddAudit(
            db,
            "role.delete",
            actor,
            roleId,
            profile.Role.Name,
            null,
            null,
            oldState,
            "{}");

        profile.IsDisabled = true;
        AccessControlJournal.Bump(profile);

        AccessControlJournal.AddRevision(
            db,
            profile,
            "delete",
            actor.Name);

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);
    }

    public async Task AssignUserAsync(
        string roleId,
        string userId,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(
                actor.Id,
                userId,
                StringComparison.Ordinal))
        {
            throw AccessControlRules.Conflict(
                "Users cannot assign roles to themselves.");
        }

        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        if (profile.IsDeleted)
        {
            throw AccessControlRules.Conflict(
                "Restore the deleted role before changing it.");
        }

        if (profile.IsDisabled)
        {
            throw AccessControlRules.Conflict(
                "Disabled roles cannot be assigned.");
        }

        AccessControlUserRow user =
            await AccessControlRules.RequireUserAsync(
                db,
                userId,
                cancellationToken);

        if (await db.UserRoles.AnyAsync(
                row =>
                    row.RoleId == roleId &&
                    row.UserId == userId,
                cancellationToken))
        {
            throw AccessControlRules.Conflict(
                "User already has this role.");
        }

        db.UserRoles.Add(
            new AccessControlUserRoleRow
            {
                RoleId = roleId,
                UserId = userId
            });

        user.SecurityStamp =
            Guid.NewGuid().ToString();

        AccessControlJournal.AddAudit(
            db,
            "membership.assign",
            actor,
            roleId,
            profile.Role.Name,
            userId,
            user.UserName ?? user.Email ?? user.Id,
            JsonSerializer.Serialize(
                new MembershipSnapshot(
                    userId,
                    roleId,
                    false)),
            JsonSerializer.Serialize(
                new MembershipSnapshot(
                    userId,
                    roleId,
                    true)));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);
    }

    public async Task RevokeUserAsync(
        string roleId,
        string userId,
        AccessControlActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            new AccessControlDbContext(_options);

        await using var transaction =
            await BeginMutationAsync(db, cancellationToken);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: true,
                cancellationToken);

        AccessControlUserRow user =
            await AccessControlRules.RequireUserAsync(
                db,
                userId,
                cancellationToken);

        AccessControlUserRoleRow membership =
            await db.UserRoles
                .SingleOrDefaultAsync(
                    row =>
                        row.RoleId == roleId &&
                        row.UserId == userId,
                    cancellationToken)
            ?? throw AccessControlRules.NotFound(
                "User does not have this role.");

        if (string.Equals(
                profile.Role.Name,
                SystemRoleCatalog.Administrator,
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(
                    actor.Id,
                    userId,
                    StringComparison.Ordinal))
            {
                throw AccessControlRules.Conflict(
                    "An administrator cannot remove their own system administrator role.");
            }

            await EnsureAdminRemainsAsync(
                db,
                roleId,
                user,
                cancellationToken);
        }

        db.UserRoles.Remove(membership);

        user.SecurityStamp =
            Guid.NewGuid().ToString();

        AccessControlJournal.AddAudit(
            db,
            "membership.revoke",
            actor,
            roleId,
            profile.Role.Name,
            userId,
            user.UserName ?? user.Email ?? user.Id,
            JsonSerializer.Serialize(
                new MembershipSnapshot(
                    userId,
                    roleId,
                    true)),
            JsonSerializer.Serialize(
                new MembershipSnapshot(
                    userId,
                    roleId,
                    false)));

        await SaveMutationAsync(
            db,
            transaction,
            cancellationToken);
    }

    private static async Task EnsureAdminRemainsAsync(
        AccessControlDbContext db,
        string adminRoleId,
        AccessControlUserRow target,
        CancellationToken cancellationToken)
    {
        string[] administratorIds =
            await db.UserRoles
                .Where(membership =>
                    membership.RoleId == adminRoleId)
                .Select(membership =>
                    membership.UserId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        AccessControlUserRow[] administrators =
            await db.Users
                .Where(user =>
                    administratorIds.Contains(user.Id))
                .ToArrayAsync(cancellationToken);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        int activeAdmins = administrators.Count(
            user =>
                !user.LockoutEnd.HasValue ||
                user.LockoutEnd <= now);

        bool targetActive =
            !target.LockoutEnd.HasValue ||
            target.LockoutEnd <= now;

        int remaining =
            activeAdmins - (targetActive ? 1 : 0);

        if (remaining < 1)
        {
            throw AccessControlRules.Conflict(
                "The last active administrator role assignment cannot be removed.");
        }
    }

    private static async Task<
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>
        BeginMutationAsync(
            AccessControlDbContext db,
            CancellationToken cancellationToken)
    {
        bool isNpgsql =
            db.Database.ProviderName?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) == true;

        IsolationLevel isolation =
            isNpgsql
                ? IsolationLevel.ReadCommitted
                : IsolationLevel.Serializable;

        var transaction =
            await db.Database.BeginTransactionAsync(
                isolation,
                cancellationToken);

        if (isNpgsql)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_xact_lock({MutationAdvisoryLock})",
                cancellationToken);
        }

        return transaction;
    }

    private static async Task SaveMutationAsync(
        AccessControlDbContext db,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw AccessControlRules.Conflict(
                "Role was changed by another request.");
        }
    }
}
