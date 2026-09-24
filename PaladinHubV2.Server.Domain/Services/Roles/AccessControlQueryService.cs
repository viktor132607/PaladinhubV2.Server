using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles;

internal sealed class AccessControlQueryService
{
    private readonly DbContextOptions<AccessControlDbContext> _options;

    internal AccessControlQueryService(
        DbContextOptions<AccessControlDbContext> options)
    {
        _options = options;
    }

    internal async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(
        CancellationToken cancellationToken)
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
            .Select(group => new
            {
                RoleId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                row => row.RoleId,
                row => row.Count,
                cancellationToken);

        return profiles
            .Select(profile => AccessControlJournal.ToResponse(
                profile,
                counts.GetValueOrDefault(profile.RoleId)))
            .ToArray();
    }

    internal async Task<RoleResponse> GetRoleAsync(
        string roleId,
        CancellationToken cancellationToken)
    {
        await using var db = new AccessControlDbContext(_options);

        RoleSecurityProfile profile =
            await AccessControlRules.RequireProfileAsync(
                db,
                roleId,
                tracking: false,
                cancellationToken);

        int userCount = await db.UserRoles
            .AsNoTracking()
            .CountAsync(
                row => row.RoleId == roleId,
                cancellationToken);

        return AccessControlJournal.ToResponse(profile, userCount);
    }

    internal Task<IReadOnlyList<PermissionDefinition>>
        GetPermissionCatalogAsync()
    {
        return Task.FromResult<IReadOnlyList<PermissionDefinition>>(
            AdminPermissions.All);
    }

    internal async Task<IReadOnlyList<RoleUserSummary>> GetRoleUsersAsync(
        string roleId,
        CancellationToken cancellationToken)
    {
        await using var db = new AccessControlDbContext(_options);

        _ = await AccessControlRules.RequireProfileAsync(
            db,
            roleId,
            tracking: false,
            cancellationToken);

        return await (
                from membership in db.UserRoles.AsNoTracking()
                join user in db.Users.AsNoTracking()
                    on membership.UserId equals user.Id
                where membership.RoleId == roleId
                orderby user.UserName
                select new RoleUserSummary(
                    user.Id,
                    user.UserName ?? user.Email ?? user.Id,
                    user.Email))
            .ToArrayAsync(cancellationToken);
    }

    internal async Task<IReadOnlyList<UserRoleSummary>> ListUsersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        await using var db = new AccessControlDbContext(_options);

        IQueryable<AccessControlUserRow> users =
            db.Users.AsNoTracking();

        string? term = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        if (term is not null)
        {
            string lowered = term.ToLower();
            users = users.Where(user =>
                (user.UserName != null &&
                 user.UserName.ToLower().Contains(lowered)) ||
                (user.Email != null &&
                 user.Email.ToLower().Contains(lowered)));
        }

        AccessControlUserRow[] userRows = await users
            .OrderBy(user => user.UserName)
            .Take(200)
            .ToArrayAsync(cancellationToken);

        string[] ids = userRows
            .Select(user => user.Id)
            .ToArray();

        var memberships = await (
                from membership in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking()
                    on membership.RoleId equals role.Id
                where ids.Contains(membership.UserId)
                select new
                {
                    membership.UserId,
                    RoleName = role.Name ?? role.Id
                })
            .ToArrayAsync(cancellationToken);

        return userRows
            .Select(user => new UserRoleSummary(
                user.Id,
                user.UserName ?? user.Email ?? user.Id,
                user.Email,
                memberships
                    .Where(row => row.UserId == user.Id)
                    .Select(row => row.RoleName)
                    .OrderBy(
                        name => name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    internal async Task<IReadOnlyList<RoleSecurityRevisionResponse>>
        GetHistoryAsync(
            string roleId,
            CancellationToken cancellationToken)
    {
        await using var db = new AccessControlDbContext(_options);

        _ = await AccessControlRules.RequireProfileAsync(
            db,
            roleId,
            tracking: false,
            cancellationToken);

        return await db.RoleSecurityRevisions
            .AsNoTracking()
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

    internal async Task<IReadOnlyList<AccessControlAuditResponse>>
        GetAuditAsync(
            string? roleId,
            string? userId,
            CancellationToken cancellationToken)
    {
        await using var db = new AccessControlDbContext(_options);

        IQueryable<AccessControlAuditEntry> query =
            db.AccessControlAuditEntries.AsNoTracking();

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

        return await query
            .OrderByDescending(entry => entry.CreatedAtUtc)
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
}
