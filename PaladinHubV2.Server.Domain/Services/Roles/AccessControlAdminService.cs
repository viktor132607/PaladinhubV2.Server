using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

public enum AccessControlFailure
{
    Validation,
    NotFound,
    Conflict
}

public sealed class AccessControlAdminException : Exception
{
    public AccessControlAdminException(AccessControlFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public AccessControlFailure Failure { get; }
}

public sealed record AccessControlActor(string Id, string Name);
public sealed record CreateRoleRequest(string Name, IReadOnlyCollection<string>? Permissions);
public sealed record UpdateRoleRequest(string Name, bool IsDisabled, int Version);
public sealed record ReplaceRolePermissionsRequest(IReadOnlyCollection<string> Permissions, int Version);
public sealed record RestoreRoleRevisionRequest(int RevisionVersion, int Version);

public sealed record RoleUserSummary(string Id, string UserName, string? Email);
public sealed record UserRoleSummary(string Id, string UserName, string? Email, IReadOnlyList<string> Roles);
public sealed record RoleSecurityRevisionResponse(int Version, string Action, string Actor, DateTime CreatedAtUtc, string Snapshot);
public sealed record AccessControlAuditResponse(
    Guid Id,
    string Action,
    string ActorId,
    string Actor,
    string? TargetRoleId,
    string? TargetRoleName,
    string? TargetUserId,
    string? TargetUserName,
    DateTime CreatedAtUtc,
    string OldState,
    string NewState);

public sealed record RoleResponse(
    string Id,
    string Name,
    bool IsSystem,
    bool IsDisabled,
    int Version,
    int UserCount,
    IReadOnlyList<string> Permissions, bool IsDeleted = false);

internal sealed record RoleSnapshot(string Name, bool IsDisabled, string[] Permissions);
internal sealed record MembershipSnapshot(string UserId, string RoleId, bool Assigned);

public sealed class AccessControlAdminService
{
    private const long MutationAdvisoryLock = 8820416;
    private readonly DbContextOptions<AccessControlDbContext> _options;

    public AccessControlAdminService(DbContextOptions<AccessControlDbContext> options)
    {
        _options = options;
    }

    public static AccessControlAdminService ForPostgres(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AccessControlAdminService(options);
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        RoleSecurityProfile[] profiles = await db.RoleSecurityProfiles
            .AsNoTracking()
            .Include(profile => profile.Role)
            .Include(profile => profile.Permissions)
            .Include(profile => profile.Revisions)
            .OrderBy(profile => profile.Role.Name)
            .ToArrayAsync(cancellationToken);

        Dictionary<string, int> counts = await db.UserRoles
            .AsNoTracking()
            .GroupBy(row => row.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.RoleId, row => row.Count, cancellationToken);

        return profiles.Select(profile => ToResponse(
                profile,
                counts.GetValueOrDefault(profile.RoleId)))
            .ToArray();
    }

    public async Task<RoleResponse> GetRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: false, cancellationToken);
        int userCount = await db.UserRoles.AsNoTracking()
            .CountAsync(row => row.RoleId == roleId, cancellationToken);
        return ToResponse(profile, userCount);
    }

    public async Task<IReadOnlyList<PermissionDefinition>> GetPermissionCatalogAsync()
    {
        await Task.CompletedTask;
        return AdminPermissions.All;
    }

    public async Task<IReadOnlyList<RoleUserSummary>> GetRoleUsersAsync(string roleId, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        _ = await RequireProfileAsync(db, roleId, tracking: false, cancellationToken);

        return await (from membership in db.UserRoles.AsNoTracking()
                      join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                      where membership.RoleId == roleId
                      orderby user.UserName
                      select new RoleUserSummary(
                          user.Id,
                          user.UserName ?? user.Email ?? user.Id,
                          user.Email))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserRoleSummary>> ListUsersAsync(string? search, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        IQueryable<AccessControlUserRow> users = db.Users.AsNoTracking();
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is not null)
        {
            string lowered = term.ToLower();
            users = users.Where(user =>
                (user.UserName != null && user.UserName.ToLower().Contains(lowered)) ||
                (user.Email != null && user.Email.ToLower().Contains(lowered)));
        }

        AccessControlUserRow[] userRows = await users
            .OrderBy(user => user.UserName)
            .Take(200)
            .ToArrayAsync(cancellationToken);
        string[] ids = userRows.Select(user => user.Id).ToArray();

        var memberships = await (from membership in db.UserRoles.AsNoTracking()
                                 join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                                 where ids.Contains(membership.UserId)
                                 select new { membership.UserId, RoleName = role.Name ?? role.Id })
            .ToArrayAsync(cancellationToken);

        return userRows.Select(user => new UserRoleSummary(
                user.Id,
                user.UserName ?? user.Email ?? user.Id,
                user.Email,
                memberships.Where(row => row.UserId == user.Id)
                    .Select(row => row.RoleName)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<RoleSecurityRevisionResponse>> GetHistoryAsync(string roleId, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        _ = await RequireProfileAsync(db, roleId, tracking: false, cancellationToken);
        return await db.RoleSecurityRevisions.AsNoTracking()
            .Where(revision => revision.RoleId == roleId)
            .OrderByDescending(revision => revision.Version)
            .Select(revision => new RoleSecurityRevisionResponse(
                revision.Version,
                revision.Action,
                revision.Actor,
                revision.CreatedAtUtc,
                revision.Snapshot))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccessControlAuditResponse>> GetAuditAsync(string? roleId, string? userId, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        IQueryable<AccessControlAuditEntry> query = db.AccessControlAuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(roleId))
        {
            string id = roleId.Trim();
            query = query.Where(entry => entry.TargetRoleId == id);
        }
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string id = userId.Trim();
            query = query.Where(entry => entry.TargetUserId == id);
        }

        return await query.OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(500)
            .Select(entry => new AccessControlAuditResponse(
                entry.Id,
                entry.Action,
                entry.ActorId,
                entry.Actor,
                entry.TargetRoleId,
                entry.TargetRoleName,
                entry.TargetUserId,
                entry.TargetUserName,
                entry.CreatedAtUtc,
                entry.OldState,
                entry.NewState))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string name = ValidateRoleName(request.Name);
        if (SystemRoleCatalog.IsSystemRole(name))
        {
            throw Conflict("System role names are reserved.");
        }
        string[] permissions = ValidatePermissions(request.Permissions ?? Array.Empty<string>());

        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        string normalizedName = NormalizeName(name);
        if (await db.Roles.AnyAsync(role => role.NormalizedName == normalizedName, cancellationToken))
        {
            throw Conflict("A role with this name already exists.");
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
        AddPermissions(db, profile, permissions, actor.Name);
        AddRevision(db, profile, "create", actor.Name);
        AddAudit(db, "role.create", actor, role.Id, name, null, null, "{}", SerializeSnapshot(profile));
        await SaveMutationAsync(db, transaction, cancellationToken);
        return ToResponse(profile, 0);
    }

    public async Task<RoleResponse> UpdateRoleAsync(string roleId, UpdateRoleRequest request, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string name = ValidateRoleName(request.Name);

        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        await AccessControlMutationGuard.EnsureRoleUpdateIsSafeAsync(db, roleId, request, actor, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        if (profile.IsDeleted) throw Conflict("Restore the deleted role before changing it.");
        RequireVersion(profile, request.Version);
        RoleSnapshot before = Snapshot(profile);
        SystemRoleDefinition? systemRole = SystemRoleCatalog.Find(profile.Role.Name);
        if (systemRole is not null)
        {
            if (systemRole.ProtectName && !string.Equals(name, systemRole.Name, StringComparison.Ordinal))
            {
                throw Conflict("System role name cannot be changed.");
            }
            if (request.IsDisabled)
            {
                throw Conflict("System administrator role cannot be disabled.");
            }
        }
        else if (SystemRoleCatalog.IsSystemRole(name))
        {
            throw Conflict("System role names are reserved.");
        }

        string normalizedName = NormalizeName(name);
        if (await db.Roles.AnyAsync(role => role.Id != roleId && role.NormalizedName == normalizedName, cancellationToken))
        {
            throw Conflict("A role with this name already exists.");
        }

        profile.Role.Name = name;
        profile.Role.NormalizedName = normalizedName;
        profile.Role.ConcurrencyStamp = Guid.NewGuid().ToString();
        profile.IsDisabled = request.IsDisabled;
        Bump(profile);
        AddRevision(db, profile, "update", actor.Name);
        AddAudit(db, "role.update", actor, roleId, name, null, null, JsonSerializer.Serialize(before), SerializeSnapshot(profile));
        await SaveMutationAsync(db, transaction, cancellationToken);
        int count = await db.UserRoles.CountAsync(row => row.RoleId == roleId, cancellationToken);
        return ToResponse(profile, count);
    }

    public async Task<RoleResponse> ReplacePermissionsAsync(string roleId, ReplaceRolePermissionsRequest request, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string[] requested = ValidatePermissions(request.Permissions);

        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        await AccessControlMutationGuard.EnsurePermissionReplacementIsSafeAsync(db, roleId, request, actor, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        if (profile.IsDeleted) throw Conflict("Restore the deleted role before changing it.");
        RequireVersion(profile, request.Version);
        RoleSnapshot before = Snapshot(profile);
        SystemRoleDefinition? systemRole = SystemRoleCatalog.Find(profile.Role.Name);
        if (systemRole?.ProtectPermissionSet == true &&
            !requested.ToHashSet(StringComparer.Ordinal).SetEquals(systemRole.RequiredPermissions))
        {
            throw Conflict("The system administrator role must keep the complete permission set.");
        }

        string[] current = profile.Permissions.Select(permission => permission.PermissionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (current.SequenceEqual(requested, StringComparer.Ordinal))
        {
            int currentCount = await db.UserRoles.CountAsync(row => row.RoleId == roleId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(profile, currentCount);
        }

        db.RolePermissions.RemoveRange(profile.Permissions);
        profile.Permissions.Clear();
        AddPermissions(db, profile, requested, actor.Name);
        Bump(profile);
        AddRevision(db, profile, "permissions", actor.Name);
        AddAudit(db, "role.permissions", actor, roleId, profile.Role.Name, null, null, JsonSerializer.Serialize(before), SerializeSnapshot(profile));
        await SaveMutationAsync(db, transaction, cancellationToken);
        int count = await db.UserRoles.CountAsync(row => row.RoleId == roleId, cancellationToken);
        return ToResponse(profile, count);
    }

    public async Task<RoleResponse> RestoreRevisionAsync(string roleId, RestoreRoleRevisionRequest request, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        await AccessControlMutationGuard.EnsureRestoreIsSafeAsync(db, roleId, request, actor, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        RequireVersion(profile, request.Version);
        RoleSecurityRevision revision = await db.RoleSecurityRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RoleId == roleId && item.Version == request.RevisionVersion, cancellationToken)
            ?? throw NotFound("Role revision was not found.");
        if (revision.Action == "delete") throw Conflict("Choose a revision from before deletion.");
        RoleSnapshot restored = JsonSerializer.Deserialize<RoleSnapshot>(revision.Snapshot)
            ?? throw Conflict("Role revision snapshot is invalid.");
        ValidateRoleName(restored.Name);
        string[] permissions = ValidatePermissions(restored.Permissions);
        RoleSnapshot before = Snapshot(profile);

        SystemRoleDefinition? systemRole = SystemRoleCatalog.Find(profile.Role.Name);
        if (systemRole is not null)
        {
            if (!string.Equals(restored.Name, systemRole.Name, StringComparison.Ordinal) || restored.IsDisabled ||
                !permissions.ToHashSet(StringComparer.Ordinal).SetEquals(systemRole.RequiredPermissions))
            {
                throw Conflict("Revision would violate protected system-role invariants.");
            }
        }
        else
        {
            if (SystemRoleCatalog.IsSystemRole(restored.Name))
            {
                throw Conflict("Revision would use a reserved system-role name.");
            }
            string normalized = NormalizeName(restored.Name);
            if (await db.Roles.AnyAsync(role => role.Id != roleId && role.NormalizedName == normalized, cancellationToken))
            {
                throw Conflict("Revision would conflict with an existing role name.");
            }
        }

        profile.Role.Name = restored.Name;
        profile.Role.NormalizedName = NormalizeName(restored.Name);
        profile.Role.ConcurrencyStamp = Guid.NewGuid().ToString();
        profile.IsDisabled = restored.IsDisabled;
        db.RolePermissions.RemoveRange(profile.Permissions);
        profile.Permissions.Clear();
        AddPermissions(db, profile, permissions, actor.Name);
        Bump(profile);
        AddRevision(db, profile, "restore", actor.Name);
        AddAudit(db, "role.restore", actor, roleId, profile.Role.Name, null, null, JsonSerializer.Serialize(before), SerializeSnapshot(profile));
        await SaveMutationAsync(db, transaction, cancellationToken);
        int count = await db.UserRoles.CountAsync(row => row.RoleId == roleId, cancellationToken);
        return ToResponse(profile, count);
    }

    public async Task DeleteRoleAsync(string roleId, int version, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        if (profile.IsDeleted) throw Conflict("Restore the deleted role before changing it.");
        RequireVersion(profile, version);
        if (profile.IsSystem || SystemRoleCatalog.IsSystemRole(profile.Role.Name))
        {
            throw Conflict("System roles cannot be deleted.");
        }
        int users = await db.UserRoles.CountAsync(row => row.RoleId == roleId, cancellationToken);
        if (users != 0)
        {
            throw Conflict("Role is still assigned to users and cannot be deleted.");
        }

        string oldState = SerializeSnapshot(profile);
        AddAudit(db, "role.delete", actor, roleId, profile.Role.Name, null, null, oldState, "{}");
        profile.IsDisabled = true;
        Bump(profile);
        AddRevision(db, profile, "delete", actor.Name);
        await SaveMutationAsync(db, transaction, cancellationToken);
    }

    public async Task AssignUserAsync(string roleId, string userId, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        if (string.Equals(actor.Id, userId, StringComparison.Ordinal))
            throw Conflict("Users cannot assign roles to themselves.");
        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        if (profile.IsDeleted) throw Conflict("Restore the deleted role before changing it.");
        if (profile.IsDisabled)
        {
            throw Conflict("Disabled roles cannot be assigned.");
        }
        AccessControlUserRow user = await RequireUserAsync(db, userId, cancellationToken);
        if (await db.UserRoles.AnyAsync(row => row.RoleId == roleId && row.UserId == userId, cancellationToken))
        {
            throw Conflict("User already has this role.");
        }

        db.UserRoles.Add(new AccessControlUserRoleRow { RoleId = roleId, UserId = userId });
        user.SecurityStamp = Guid.NewGuid().ToString();
        AddAudit(
            db,
            "membership.assign",
            actor,
            roleId,
            profile.Role.Name,
            userId,
            user.UserName ?? user.Email ?? user.Id,
            JsonSerializer.Serialize(new MembershipSnapshot(userId, roleId, false)),
            JsonSerializer.Serialize(new MembershipSnapshot(userId, roleId, true)));
        await SaveMutationAsync(db, transaction, cancellationToken);
    }

    public async Task RevokeUserAsync(string roleId, string userId, AccessControlActor actor, CancellationToken cancellationToken = default)
    {
        await using var db = new AccessControlDbContext(_options);
        await using var transaction = await BeginMutationAsync(db, cancellationToken);
        RoleSecurityProfile profile = await RequireProfileAsync(db, roleId, tracking: true, cancellationToken);
        AccessControlUserRow user = await RequireUserAsync(db, userId, cancellationToken);
        AccessControlUserRoleRow membership = await db.UserRoles
            .SingleOrDefaultAsync(row => row.RoleId == roleId && row.UserId == userId, cancellationToken)
            ?? throw NotFound("User does not have this role.");

        if (string.Equals(profile.Role.Name, SystemRoleCatalog.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(actor.Id, userId, StringComparison.Ordinal))
            {
                throw Conflict("An administrator cannot remove their own system administrator role.");
            }
            await EnsureAdminRemainsAsync(db, roleId, user, cancellationToken);
        }

        db.UserRoles.Remove(membership);
        user.SecurityStamp = Guid.NewGuid().ToString();
        AddAudit(
            db,
            "membership.revoke",
            actor,
            roleId,
            profile.Role.Name,
            userId,
            user.UserName ?? user.Email ?? user.Id,
            JsonSerializer.Serialize(new MembershipSnapshot(userId, roleId, true)),
            JsonSerializer.Serialize(new MembershipSnapshot(userId, roleId, false)));
        await SaveMutationAsync(db, transaction, cancellationToken);
    }

    private static async Task EnsureAdminRemainsAsync(
        AccessControlDbContext db,
        string adminRoleId,
        AccessControlUserRow target,
        CancellationToken cancellationToken)
    {
        string[] administratorIds = await db.UserRoles
            .Where(membership => membership.RoleId == adminRoleId)
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        AccessControlUserRow[] administrators = await db.Users
            .Where(user => administratorIds.Contains(user.Id))
            .ToArrayAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int activeAdmins = administrators.Count(user =>
            !user.LockoutEnd.HasValue || user.LockoutEnd <= now);
        bool targetActive = !target.LockoutEnd.HasValue || target.LockoutEnd <= now;
        int remaining = activeAdmins - (targetActive ? 1 : 0);
        if (remaining < 1)
        {
            throw Conflict("The last active administrator role assignment cannot be removed.");
        }
    }

    private static string ValidateRoleName(string? value)
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
            throw Validation("Role name contains invalid control characters.");
        }
        return name;
    }

    private static string[] ValidatePermissions(IEnumerable<string>? values)
    {
        string[] permissions = (values ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] unknown = permissions.Where(permission => !AdminPermissions.IsKnown(permission)).ToArray();
        if (unknown.Length != 0)
        {
            throw Validation($"Unknown permission(s): {string.Join(", ", unknown)}.");
        }
        return permissions;
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static async Task<RoleSecurityProfile> RequireProfileAsync(
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
        return await query.SingleOrDefaultAsync(profile => profile.RoleId == roleId, cancellationToken)
            ?? throw NotFound("Role was not found.");
    }

    private static async Task<AccessControlUserRow> RequireUserAsync(
        AccessControlDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw Validation("User ID is required.");
        }
        return await db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw NotFound("User was not found.");
    }

    private static void RequireVersion(RoleSecurityProfile profile, int expectedVersion)
    {
        if (expectedVersion <= 0)
        {
            throw Validation("A positive role version is required.");
        }
        if (profile.Version != expectedVersion)
        {
            throw Conflict($"Role was changed by another request. Current version is {profile.Version}.");
        }
    }

    private static void AddPermissions(
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

    private static void Bump(RoleSecurityProfile profile)
    {
        checked { profile.Version++; }
        profile.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static RoleSnapshot Snapshot(RoleSecurityProfile profile)
    {
        return new RoleSnapshot(
            profile.Role.Name ?? profile.RoleId,
            profile.IsDisabled,
            profile.Permissions.Select(permission => permission.PermissionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    private static string SerializeSnapshot(RoleSecurityProfile profile) => JsonSerializer.Serialize(Snapshot(profile));

    private static void AddRevision(AccessControlDbContext db, RoleSecurityProfile profile, string action, string actor)
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

    private static void AddAudit(
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

    private static RoleResponse ToResponse(RoleSecurityProfile profile, int userCount)
    {
        SystemRoleDefinition? system = SystemRoleCatalog.Find(profile.Role.Name);
        IReadOnlyList<string> permissions = system?.ProtectPermissionSet == true
            ? system.RequiredPermissions.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : profile.Permissions.Select(permission => permission.PermissionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        return new RoleResponse(
            profile.RoleId,
            profile.Role.Name ?? profile.RoleId,
            profile.IsSystem || system is not null,
            profile.IsDisabled,
            profile.Version,
            userCount,
            permissions, profile.IsDeleted);
    }

    private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginMutationAsync(
        AccessControlDbContext db,
        CancellationToken cancellationToken)
    {
        bool isNpgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        IsolationLevel isolation = isNpgsql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
        var transaction = await db.Database.BeginTransactionAsync(isolation, cancellationToken);
        if (isNpgsql)
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({MutationAdvisoryLock})", cancellationToken);
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
            throw Conflict("Role was changed by another request.");
        }
    }

    private static AccessControlAdminException Validation(string message) => new(AccessControlFailure.Validation, message);
    private static AccessControlAdminException NotFound(string message) => new(AccessControlFailure.NotFound, message);
    private static AccessControlAdminException Conflict(string message) => new(AccessControlFailure.Conflict, message);
}
