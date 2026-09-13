using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlRoleManagementTests : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AccessControlDbContext> options;
    private readonly AccessControlAdminService service;
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly AccessControlActor Actor = new("root", "Root Admin");

    public AccessControlRoleManagementTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<AccessControlDbContext>().UseSqlite(connection).Options;
        using var db = new AccessControlDbContext(options);
        db.Database.EnsureCreated();
        SeedAdmin(db);
        db.SaveChanges();
        service = new AccessControlAdminService(options);
    }

    [Fact]
    public async Task CreateRolePersistsPermissionsRevisionAndAudit()
    {
        RoleResponse role = await service.CreateRoleAsync(
            new CreateRoleRequest("  Editor  ", [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update]), Actor, Ct);

        Assert.Equal("Editor", role.Name);
        Assert.Equal(1, role.Version);
        Assert.Equal([AdminPermissions.Pages.Read, AdminPermissions.Pages.Update], role.Permissions);

        await using var db = new AccessControlDbContext(options);
        Assert.Single(await db.RoleSecurityRevisions.Where(x => x.RoleId == role.Id).ToListAsync(Ct));
        Assert.Contains(await db.AccessControlAuditEntries.ToListAsync(Ct), x => x.Action == "role.create");
    }

    [Fact]
    public async Task DuplicateNameUnknownPermissionAndStaleVersionAreRejected()
    {
        RoleResponse role = await service.CreateRoleAsync(new CreateRoleRequest("Editor", []), Actor, Ct);

        Assert.Equal(AccessControlFailure.Conflict,
            (await Assert.ThrowsAsync<AccessControlAdminException>(() =>
                service.CreateRoleAsync(new CreateRoleRequest("editor", []), Actor, Ct))).Failure);
        Assert.Equal(AccessControlFailure.Validation,
            (await Assert.ThrowsAsync<AccessControlAdminException>(() =>
                service.CreateRoleAsync(new CreateRoleRequest("Other", ["pages.superuser"]), Actor, Ct))).Failure);

        RoleResponse updated = await service.UpdateRoleAsync(
            role.Id, new UpdateRoleRequest("Senior Editor", false, role.Version), Actor, Ct);
        Assert.Equal(2, updated.Version);
        Assert.Equal(AccessControlFailure.Conflict,
            (await Assert.ThrowsAsync<AccessControlAdminException>(() =>
                service.UpdateRoleAsync(role.Id, new UpdateRoleRequest("Stale", false, role.Version), Actor, Ct))).Failure);
    }

    [Fact]
    public async Task ProtectedAdminCannotBeRenamedDisabledRepermissionedOrDeleted()
    {
        RoleResponse admin = (await service.ListRolesAsync(Ct)).Single(x => x.Name == "Admin");

        await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.UpdateRoleAsync(admin.Id, new UpdateRoleRequest("Owner", false, admin.Version), Actor, Ct));
        await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.UpdateRoleAsync(admin.Id, new UpdateRoleRequest("Admin", true, admin.Version), Actor, Ct));
        await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.ReplacePermissionsAsync(admin.Id,
                new ReplaceRolePermissionsRequest([AdminPermissions.Pages.Read], admin.Version), Actor, Ct));
        await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.DeleteRoleAsync(admin.Id, admin.Version, Actor, Ct));
    }

    [Fact]
    public async Task RevisionRestoreCreatesNewVersionAndRestoresSnapshot()
    {
        RoleResponse role = await service.CreateRoleAsync(
            new CreateRoleRequest("Editor", [AdminPermissions.Pages.Read]), Actor, Ct);
        RoleResponse updated = await service.UpdateRoleAsync(
            role.Id, new UpdateRoleRequest("Senior Editor", false, role.Version), Actor, Ct);

        RoleResponse restored = await service.RestoreRevisionAsync(
            role.Id, new RestoreRoleRevisionRequest(1, updated.Version), Actor, Ct);

        Assert.Equal("Editor", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Equal([AdminPermissions.Pages.Read], restored.Permissions);
    }

    private static void SeedAdmin(AccessControlDbContext db)
    {
        var role = new IdentityRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        db.Roles.Add(role);
        db.RoleSecurityProfiles.Add(new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role,
            IsSystem = true,
            Version = 1
        });
        db.Users.Add(new AccessControlUserRow
        {
            Id = "admin-1",
            UserName = "admin-1",
            NormalizedUserName = "ADMIN-1",
            Email = "admin-1@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true
        });
        db.UserRoles.Add(new AccessControlUserRoleRow { UserId = "admin-1", RoleId = role.Id });
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
