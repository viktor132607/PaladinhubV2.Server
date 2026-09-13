using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlModelTests
{
    [Fact]
    public async Task AccessControlModelPersistsRoleProfilePermissionsAndHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<AccessControlDbContext> options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db = new AccessControlDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var role = new IdentityRole("Editor") { Id = "role-editor" };
        db.Add(role);
        db.RoleSecurityProfiles.Add(new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role,
            IsSystem = false,
            IsDisabled = false,
            Version = 1,
            Permissions =
            {
                new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = AdminPermissions.Pages.Read,
                    GrantedBy = "test"
                }
            },
            Revisions =
            {
                new RoleSecurityRevision
                {
                    RoleId = role.Id,
                    Version = 1,
                    Action = "create",
                    Actor = "test",
                    Snapshot = "{}"
                }
            }
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        RoleSecurityProfile stored = await db.RoleSecurityProfiles
            .Include(profile => profile.Permissions)
            .Include(profile => profile.Revisions)
            .SingleAsync(profile => profile.RoleId == role.Id, TestContext.Current.CancellationToken);

        Assert.False(stored.IsSystem);
        Assert.False(stored.IsDisabled);
        Assert.Equal(1, stored.Version);
        Assert.Single(stored.Permissions);
        Assert.Equal(AdminPermissions.Pages.Read, stored.Permissions.Single().PermissionId);
        Assert.Single(stored.Revisions);
    }

    [Fact]
    public void RoleSecurityVersionIsConcurrencyTokenAndDeleteBehaviorsAreExplicit()
    {
        DbContextOptions<AccessControlDbContext> options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

        using var db = new AccessControlDbContext(options);
        var profile = db.Model.FindEntityType(typeof(RoleSecurityProfile))!;
        var permission = db.Model.FindEntityType(typeof(RolePermission))!;
        var revision = db.Model.FindEntityType(typeof(RoleSecurityRevision))!;

        Assert.True(profile.FindProperty(nameof(RoleSecurityProfile.Version))!.IsConcurrencyToken);
        Assert.Equal(
            DeleteBehavior.Restrict,
            profile.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(
            DeleteBehavior.Cascade,
            permission.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(
            DeleteBehavior.Restrict,
            revision.GetForeignKeys().Single().DeleteBehavior);
    }

    [Fact]
    public async Task DuplicatePermissionForRoleIsRejectedByCompositePrimaryKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        DbContextOptions<AccessControlDbContext> options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db = new AccessControlDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var role = new IdentityRole("Editor") { Id = "role-editor" };
        db.Add(role);
        db.RoleSecurityProfiles.Add(new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.RolePermissions.Add(new RolePermission
        {
            RoleId = role.Id,
            PermissionId = AdminPermissions.Pages.Read,
            GrantedBy = "test"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        db.RolePermissions.Add(new RolePermission
        {
            RoleId = role.Id,
            PermissionId = AdminPermissions.Pages.Read,
            GrantedBy = "test-2"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
