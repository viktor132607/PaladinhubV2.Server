using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task RulesCoverValidationLookupAndConcurrencyChecks()
    {
        Assert.Equal(
            "Editor",
            AccessControlRules.ValidateRoleName("  Editor  "));

        AssertFailure(
            AccessControlFailure.Validation,
            () => AccessControlRules.ValidateRoleName(null));
        AssertFailure(
            AccessControlFailure.Validation,
            () => AccessControlRules.ValidateRoleName(
                new string('x', 257)));
        AssertFailure(
            AccessControlFailure.Validation,
            () => AccessControlRules.ValidateRoleName(
                "Bad\nName"));

        string[] permissions =
            AccessControlRules.ValidatePermissions(
                [
                    " ",
                    AdminPermissions.Pages.Update,
                    AdminPermissions.Pages.Read,
                    AdminPermissions.Pages.Read
                ]);

        Assert.Equal(
            [
                AdminPermissions.Pages.Read,
                AdminPermissions.Pages.Update
            ],
            permissions);

        Assert.Empty(
            AccessControlRules.ValidatePermissions(null));

        AssertFailure(
            AccessControlFailure.Validation,
            () => AccessControlRules.ValidatePermissions(
                ["unknown.permission"]));

        Assert.Equal(
            "EDITOR",
            AccessControlRules.NormalizeName("Editor"));

        await using SqliteConnection connection =
            await OpenDatabaseAsync();

        var options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new AccessControlDbContext(options);

        var role = new IdentityRole
        {
            Id = "role-editor",
            Name = "Editor",
            NormalizedName = "EDITOR"
        };

        var profile = new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role,
            Version = 2
        };

        db.Roles.Add(role);
        db.RoleSecurityProfiles.Add(profile);
        db.Users.Add(new AccessControlUserRow
        {
            Id = "user-1",
            UserName = "user-1"
        });

        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            profile.RoleId,
            (await AccessControlRules.RequireProfileAsync(
                db,
                role.Id,
                tracking: true,
                Ct)).RoleId);

        Assert.Equal(
            profile.RoleId,
            (await AccessControlRules.RequireProfileAsync(
                db,
                role.Id,
                tracking: false,
                Ct)).RoleId);

        Assert.Equal(
            "user-1",
            (await AccessControlRules.RequireUserAsync(
                db,
                "user-1",
                Ct)).Id);

        await AssertFailureAsync(
            AccessControlFailure.Validation,
            () => AccessControlRules.RequireProfileAsync(
                db,
                " ",
                false,
                Ct));
        await AssertFailureAsync(
            AccessControlFailure.NotFound,
            () => AccessControlRules.RequireProfileAsync(
                db,
                "missing",
                false,
                Ct));
        await AssertFailureAsync(
            AccessControlFailure.Validation,
            () => AccessControlRules.RequireUserAsync(
                db,
                "",
                Ct));
        await AssertFailureAsync(
            AccessControlFailure.NotFound,
            () => AccessControlRules.RequireUserAsync(
                db,
                "missing",
                Ct));

        AccessControlRules.RequireVersion(profile, 2);

        AssertFailure(
            AccessControlFailure.Validation,
            () => AccessControlRules.RequireVersion(
                profile,
                0));

        AssertFailure(
            AccessControlFailure.Conflict,
            () => AccessControlRules.RequireVersion(
                profile,
                1));

        Assert.Equal(
            AccessControlFailure.Validation,
            AccessControlRules.Validation("x").Failure);
        Assert.Equal(
            AccessControlFailure.NotFound,
            AccessControlRules.NotFound("x").Failure);
        Assert.Equal(
            AccessControlFailure.Conflict,
            AccessControlRules.Conflict("x").Failure);
    }

    [Fact]
    public async Task JournalCoversPermissionsSnapshotsRevisionsAuditsAndResponses()
    {
        await using SqliteConnection connection =
            await OpenDatabaseAsync();

        var options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new AccessControlDbContext(options);

        var role = new IdentityRole
        {
            Id = "role-editor",
            Name = "Editor",
            NormalizedName = "EDITOR"
        };

        var profile = new RoleSecurityProfile
        {
            RoleId = role.Id,
            Role = role,
            Version = 1
        };

        db.Roles.Add(role);
        db.RoleSecurityProfiles.Add(profile);

        AccessControlJournal.AddPermissions(
            db,
            profile,
            [
                AdminPermissions.Pages.Update,
                AdminPermissions.Pages.Read
            ],
            "tester");

        Assert.Equal(2, profile.Permissions.Count);
        Assert.All(
            profile.Permissions,
            permission =>
                Assert.Equal("tester", permission.GrantedBy));

        RoleSnapshot snapshot =
            AccessControlJournal.Snapshot(profile);

        Assert.Equal("Editor", snapshot.Name);
        Assert.Equal(
            [
                AdminPermissions.Pages.Read,
                AdminPermissions.Pages.Update
            ],
            snapshot.Permissions);

        string serialized =
            AccessControlJournal.SerializeSnapshot(profile);

        Assert.Contains("Editor", serialized);

        DateTime before =
            profile.UpdatedAtUtc;

        AccessControlJournal.Bump(profile);

        Assert.Equal(2, profile.Version);
        Assert.True(profile.UpdatedAtUtc >= before);

        AccessControlJournal.AddRevision(
            db,
            profile,
            "update",
            "tester");

        AccessControlJournal.AddAudit(
            db,
            "role.update",
            new AccessControlActor("actor-1", "Actor"),
            role.Id,
            role.Name,
            "user-1",
            "User",
            "{}",
            serialized);

        await db.SaveChangesAsync(Ct);

        Assert.Single(db.RoleSecurityRevisions);
        Assert.Single(db.AccessControlAuditEntries);

        RoleResponse response =
            AccessControlJournal.ToResponse(
                profile,
                4);

        Assert.Equal("Editor", response.Name);
        Assert.False(response.IsSystem);
        Assert.Equal(4, response.UserCount);
        Assert.Equal(snapshot.Permissions, response.Permissions);

        var unnamedRole = new IdentityRole
        {
            Id = "role-unnamed",
            Name = null,
            NormalizedName = null
        };

        var unnamedProfile = new RoleSecurityProfile
        {
            RoleId = unnamedRole.Id,
            Role = unnamedRole,
            Version = 1
        };

        Assert.Equal(
            unnamedRole.Id,
            AccessControlJournal
                .Snapshot(unnamedProfile)
                .Name);

        Assert.Equal(
            unnamedRole.Id,
            AccessControlJournal
                .ToResponse(unnamedProfile, 0)
                .Name);

        var adminRole = new IdentityRole
        {
            Id = "role-admin",
            Name = SystemRoleCatalog.Administrator,
            NormalizedName = "ADMIN"
        };

        var adminProfile = new RoleSecurityProfile
        {
            RoleId = adminRole.Id,
            Role = adminRole,
            IsSystem = false,
            Version = 1
        };

        RoleResponse adminResponse =
            AccessControlJournal.ToResponse(
                adminProfile,
                1);

        Assert.True(adminResponse.IsSystem);
        Assert.Equal(
            SystemRoleCatalog
                .Find(SystemRoleCatalog.Administrator)!
                .RequiredPermissions
                .OrderBy(value => value, StringComparer.Ordinal),
            adminResponse.Permissions);
    }

    [Fact]
    public async Task QueryServiceCoversAllReadModelsAndFilters()
    {
        await using SqliteConnection connection =
            await OpenDatabaseAsync();

        var options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using (var db =
                     new AccessControlDbContext(options))
        {
            var editorRole = new IdentityRole
            {
                Id = "role-editor",
                Name = "Editor",
                NormalizedName = "EDITOR"
            };

            var viewerRole = new IdentityRole
            {
                Id = "role-viewer",
                Name = "Viewer",
                NormalizedName = "VIEWER"
            };

            var editorProfile = new RoleSecurityProfile
            {
                RoleId = editorRole.Id,
                Role = editorRole,
                Version = 2
            };

            var viewerProfile = new RoleSecurityProfile
            {
                RoleId = viewerRole.Id,
                Role = viewerRole,
                Version = 1
            };

            db.Roles.AddRange(editorRole, viewerRole);
            db.RoleSecurityProfiles.AddRange(
                editorProfile,
                viewerProfile);

            AccessControlJournal.AddPermissions(
                db,
                editorProfile,
                [AdminPermissions.Pages.Read],
                "seed");

            db.Users.AddRange(
                new AccessControlUserRow
                {
                    Id = "user-email",
                    UserName = null,
                    Email = "match@example.com"
                },
                new AccessControlUserRow
                {
                    Id = "user-id",
                    UserName = null,
                    Email = null
                });

            db.UserRoles.AddRange(
                new AccessControlUserRoleRow
                {
                    UserId = "user-email",
                    RoleId = editorRole.Id
                },
                new AccessControlUserRoleRow
                {
                    UserId = "user-email",
                    RoleId = viewerRole.Id
                },
                new AccessControlUserRoleRow
                {
                    UserId = "user-id",
                    RoleId = editorRole.Id
                });

            db.RoleSecurityRevisions.AddRange(
                new RoleSecurityRevision
                {
                    RoleId = editorRole.Id,
                    Profile = editorProfile,
                    Version = 1,
                    Action = "create",
                    Actor = "seed",
                    Snapshot = "{}",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
                },
                new RoleSecurityRevision
                {
                    RoleId = editorRole.Id,
                    Profile = editorProfile,
                    Version = 2,
                    Action = "update",
                    Actor = "seed",
                    Snapshot = "{}",
                    CreatedAtUtc = DateTime.UtcNow
                });

            db.AccessControlAuditEntries.AddRange(
                new AccessControlAuditEntry
                {
                    Action = "role.update",
                    ActorId = "actor",
                    Actor = "Actor",
                    TargetRoleId = editorRole.Id,
                    TargetUserId = "user-email",
                    OldState = "{}",
                    NewState = "{}",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new AccessControlAuditEntry
                {
                    Action = "role.create",
                    ActorId = "actor",
                    Actor = "Actor",
                    TargetRoleId = viewerRole.Id,
                    OldState = "{}",
                    NewState = "{}",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
                });

            await db.SaveChangesAsync(Ct);
        }

        var queries =
            new AccessControlQueryService(options);

        IReadOnlyList<RoleResponse> roles =
            await queries.ListRolesAsync(Ct);

        Assert.Equal(2, roles.Count);
        Assert.Equal(
            2,
            roles.Single(role => role.Id == "role-editor")
                .UserCount);

        RoleResponse editor =
            await queries.GetRoleAsync(
                "role-editor",
                Ct);

        Assert.Equal(2, editor.UserCount);

        Assert.Same(
            AdminPermissions.All,
            await queries.GetPermissionCatalogAsync());

        IReadOnlyList<RoleUserSummary> roleUsers =
            await queries.GetRoleUsersAsync(
                "role-editor",
                Ct);

        Assert.Contains(
            roleUsers,
            user =>
                user.Id == "user-email" &&
                user.UserName == "match@example.com");

        Assert.Contains(
            roleUsers,
            user =>
                user.Id == "user-id" &&
                user.UserName == "user-id");

        IReadOnlyList<UserRoleSummary> allUsers =
            await queries.ListUsersAsync(null, Ct);

        Assert.Equal(2, allUsers.Count);
        Assert.Equal(
            ["Editor", "Viewer"],
            allUsers
                .Single(user => user.Id == "user-email")
                .Roles);

        IReadOnlyList<UserRoleSummary> searched =
            await queries.ListUsersAsync(
                " MATCH@EXAMPLE ",
                Ct);

        Assert.Single(searched);
        Assert.Equal("user-email", searched[0].Id);

        Assert.Empty(
            await queries.ListUsersAsync(
                "does-not-exist",
                Ct));

        IReadOnlyList<RoleSecurityRevisionResponse> history =
            await queries.GetHistoryAsync(
                "role-editor",
                Ct);

        Assert.Equal([2, 1], history.Select(item => item.Version));

        Assert.Equal(
            2,
            (await queries.GetAuditAsync(null, null, Ct)).Count);

        Assert.Single(
            await queries.GetAuditAsync(
                " role-editor ",
                null,
                Ct));

        Assert.Single(
            await queries.GetAuditAsync(
                null,
                " user-email ",
                Ct));

        Assert.Single(
            await queries.GetAuditAsync(
                " role-editor ",
                " user-email ",
                Ct));
    }

    [Fact]
    public void AdminServicePostgresFactoryValidatesConnectionString()
    {
        Assert.Throws<ArgumentException>(
            () => AccessControlAdminService.ForPostgres(" "));

        AccessControlAdminService service =
            AccessControlAdminService.ForPostgres(
                "Host=localhost;Database=test;Username=test;Password=test");

        Assert.NotNull(service);
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync(Ct);

        var options =
            new DbContextOptionsBuilder<AccessControlDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new AccessControlDbContext(options);

        await db.Database.EnsureCreatedAsync(Ct);

        return connection;
    }

    private static void AssertFailure(
        AccessControlFailure failure,
        Action action)
    {
        AccessControlAdminException error =
            Assert.Throws<AccessControlAdminException>(action);

        Assert.Equal(failure, error.Failure);
    }

    private static async Task AssertFailureAsync<T>(
        AccessControlFailure failure,
        Func<Task<T>> action)
    {
        AccessControlAdminException error =
            await Assert.ThrowsAsync<AccessControlAdminException>(
                async () => await action());

        Assert.Equal(failure, error.Failure);
    }
}
