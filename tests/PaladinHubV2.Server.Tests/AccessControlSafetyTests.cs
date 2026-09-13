using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlSafetyTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AccessControlDbContext> _options;
    private readonly AccessControlAdminService _service;
    private readonly AccessControlMutationGuard _guard;
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly AccessControlActor Root = new("root", "Root Admin");
    private static readonly AccessControlActor EditorActor = new("editor-user", "Editor User");

    public AccessControlSafetyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var db = new AccessControlDbContext(_options);
        db.Database.EnsureCreated();
        db.Users.Add(new AccessControlUserRow
        {
            Id = EditorActor.Id,
            UserName = "editor-user",
            NormalizedUserName = "EDITOR-USER",
            Email = "editor@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true
        });
        db.SaveChanges();
        _service = new AccessControlAdminService(_options);
        _guard = new AccessControlMutationGuard(_options);
    }

    [Fact]
    public async Task UserCannotAssignRoleToThemselves()
    {
        RoleResponse role = await _service.CreateRoleAsync(
            new CreateRoleRequest("Editor", [AdminPermissions.Pages.Read]), Root, Ct);

        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            _guard.EnsureAssignmentIsSafeAsync(role.Id, EditorActor.Id, EditorActor, Ct));

        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    [Fact]
    public async Task UserCannotAddPermissionsToRoleTheyAlreadyHold()
    {
        RoleResponse role = await CreateAssignedRoleAsync(
            "Editor", [AdminPermissions.Pages.Read]);

        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            _guard.EnsurePermissionReplacementIsSafeAsync(
                role.Id,
                new ReplaceRolePermissionsRequest(
                    [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update], role.Version),
                EditorActor,
                Ct));

        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    [Fact]
    public async Task UserMayReducePermissionsOnRoleTheyAlreadyHold()
    {
        RoleResponse role = await CreateAssignedRoleAsync(
            "Editor", [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update]);

        await _guard.EnsurePermissionReplacementIsSafeAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([AdminPermissions.Pages.Read], role.Version),
            EditorActor,
            Ct);
    }

    [Fact]
    public async Task UserCannotReEnableDisabledRoleTheyAlreadyHold()
    {
        RoleResponse role = await CreateAssignedRoleAsync(
            "Editor", [AdminPermissions.Pages.Read]);
        RoleResponse disabled = await _service.UpdateRoleAsync(
            role.Id,
            new UpdateRoleRequest(role.Name, true, role.Version),
            Root,
            Ct);

        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            _guard.EnsureRoleUpdateIsSafeAsync(
                role.Id,
                new UpdateRoleRequest(role.Name, false, disabled.Version),
                EditorActor,
                Ct));

        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    [Fact]
    public async Task UserCannotRestoreAdditionalPermissionsOntoOwnRole()
    {
        RoleResponse role = await CreateAssignedRoleAsync(
            "Editor", [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update]);
        RoleResponse reduced = await _service.ReplacePermissionsAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([AdminPermissions.Pages.Read], role.Version),
            Root,
            Ct);

        AccessControlAdminException error = await Assert.ThrowsAsync<AccessControlAdminException>(() =>
            _guard.EnsureRestoreIsSafeAsync(
                role.Id,
                new RestoreRoleRevisionRequest(1, reduced.Version),
                EditorActor,
                Ct));

        Assert.Equal(AccessControlFailure.Conflict, error.Failure);
    }

    [Fact]
    public async Task NonMemberAdministratorMayManageRoleNormally()
    {
        RoleResponse role = await _service.CreateRoleAsync(
            new CreateRoleRequest("Editor", [AdminPermissions.Pages.Read]), Root, Ct);

        await _guard.EnsurePermissionReplacementIsSafeAsync(
            role.Id,
            new ReplaceRolePermissionsRequest(
                [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update], role.Version),
            Root,
            Ct);
    }

    private async Task<RoleResponse> CreateAssignedRoleAsync(
        string name,
        IReadOnlyCollection<string> permissions)
    {
        RoleResponse role = await _service.CreateRoleAsync(
            new CreateRoleRequest(name, permissions), Root, Ct);
        await _service.AssignUserAsync(role.Id, EditorActor.Id, Root, Ct);
        return role;
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
