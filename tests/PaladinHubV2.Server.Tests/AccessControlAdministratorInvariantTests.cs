using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlAdministratorInvariantTests : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AccessControlDbContext> options;
    private readonly AccessControlAdminService service;
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    public AccessControlAdministratorInvariantTests()
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
    public async Task AdministratorCannotRemoveOwnProtectedMembership()
    {
        RoleResponse admin = (await service.ListRolesAsync(Ct)).Single(x => x.Name == "Admin");
        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.RevokeUserAsync(admin.Id, "admin-1", new AccessControlActor("admin-1", "Admin One"), Ct));
        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    [Fact]
    public async Task FinalActiveAdministratorMembershipIsPreserved()
    {
        RoleResponse admin = (await service.ListRolesAsync(Ct)).Single(x => x.Name == "Admin");
        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.RevokeUserAsync(admin.Id, "admin-1", new AccessControlActor("root", "Root"), Ct));
        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
        Assert.Single(await service.GetRoleUsersAsync(admin.Id, Ct));
    }

    [Fact]
    public async Task WithTwoActiveAdministratorsOneMembershipMayBeRemoved()
    {
        await using (var db = new AccessControlDbContext(options))
        {
            AddUser(db, "admin-2");
            db.UserRoles.Add(new AccessControlUserRoleRow { UserId = "admin-2", RoleId = "role-admin" });
            await db.SaveChangesAsync(Ct);
        }

        await service.RevokeUserAsync("role-admin", "admin-1", new AccessControlActor("root", "Root"), Ct);
        Assert.Single(await service.GetRoleUsersAsync("role-admin", Ct));

        await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            service.RevokeUserAsync("role-admin", "admin-2", new AccessControlActor("root", "Root"), Ct));
    }

    private static void AddUser(AccessControlDbContext db, string id)
    {
        db.Users.Add(new AccessControlUserRow
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = id + "@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true
        });
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
