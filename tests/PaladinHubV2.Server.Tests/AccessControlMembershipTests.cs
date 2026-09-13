using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlMembershipTests : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AccessControlDbContext> options;
    private readonly AccessControlAdminService service;
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly AccessControlActor Root = new("root", "Root");

    public AccessControlMembershipTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<AccessControlDbContext>().UseSqlite(connection).Options;
        using var db = new AccessControlDbContext(options);
        db.Database.EnsureCreated();
        var role = new IdentityRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        db.Roles.Add(role);
        db.RoleSecurityProfiles.Add(new RoleSecurityProfile { RoleId = role.Id, Role = role, IsSystem = true, Version = 1 });
        AddUser(db, "admin-1");
        db.UserRoles.Add(new AccessControlUserRoleRow { UserId = "admin-1", RoleId = role.Id });
        db.SaveChanges();
        service = new AccessControlAdminService(options);
    }

    [Fact]
    public async Task AssignmentLifecycleWritesAuditAndChangesSecurityStamp()
    {
        RoleResponse role = await service.CreateRoleAsync(new CreateRoleRequest("Editor", []), Root, Ct);
        await AddUserAsync("editor-user");
        string before = await StampAsync("editor-user");

        await service.AssignUserAsync(role.Id, "editor-user", Root, Ct);
        string afterAssign = await StampAsync("editor-user");
        Assert.NotEqual(before, afterAssign);
        Assert.Single(await service.GetRoleUsersAsync(role.Id, Ct));

        await service.RevokeUserAsync(role.Id, "editor-user", Root, Ct);
        Assert.NotEqual(afterAssign, await StampAsync("editor-user"));
        Assert.Empty(await service.GetRoleUsersAsync(role.Id, Ct));
        IReadOnlyList<AccessControlAuditResponse> audit = await service.GetAuditAsync(role.Id, "editor-user", Ct);
        Assert.Contains(audit, x => x.Action == "membership.assign");
        Assert.Contains(audit, x => x.Action == "membership.revoke");
    }

    [Fact]
    public async Task AssignedRoleCannotBeDeleted()
    {
        RoleResponse role = await service.CreateRoleAsync(new CreateRoleRequest("Editor", []), Root, Ct);
        await AddUserAsync("editor-user");
        await service.AssignUserAsync(role.Id, "editor-user", Root, Ct);

        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.DeleteRoleAsync(role.Id, role.Version, Root, Ct));
        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    private static void AddUser(AccessControlDbContext db, string id)
    {
        db.Users.Add(new AccessControlUserRow
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = $"{id}@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true
        });
    }

    private async Task AddUserAsync(string id)
    {
        await using var db = new AccessControlDbContext(options);
        AddUser(db, id);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<string> StampAsync(string id)
    {
        await using var db = new AccessControlDbContext(options);
        return (await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, Ct)).SecurityStamp ?? string.Empty;
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
