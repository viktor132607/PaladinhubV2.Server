using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class EffectivePermissionServicePostgresTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EffectivePermissionsMatchSystemCustomManageAndDisabledRoleSemantics()
    {
        string? rootConnectionString = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(rootConnectionString))
        {
            Assert.Skip("Set SEO_POSTGRES_CONNECTION to run PostgreSQL integration tests.");
            return;
        }

        string databaseName = "effective_permissions_" + Guid.NewGuid().ToString("N");
        string connectionString = await CreateDatabaseAsync(rootConnectionString, databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var database = new AppDbContext(options);
            await database.Database.EnsureCreatedAsync(Ct);

            database.Roles.AddRange(
                Role("role-admin", SystemRoleCatalog.Administrator),
                Role("role-editor", "Editor"),
                Role("role-manager", "Role Manager"),
                Role("role-disabled", "Disabled Editor"));
            database.Users.AddRange(
                User("user-admin"),
                User("user-editor"),
                User("user-manager"),
                User("user-disabled"),
                User("user-ordinary"));
            database.UserRoles.AddRange(
                new IdentityUserRole<string> { UserId = "user-admin", RoleId = "role-admin" },
                new IdentityUserRole<string> { UserId = "user-editor", RoleId = "role-editor" },
                new IdentityUserRole<string> { UserId = "user-manager", RoleId = "role-manager" },
                new IdentityUserRole<string> { UserId = "user-disabled", RoleId = "role-disabled" });
            await database.SaveChangesAsync(Ct);

            await ExecuteUpgradeAsync(connectionString);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync(Ct);
                await ExecuteAsync(connection,
                    "INSERT INTO \"RolePermissions\" (\"RoleId\", \"PermissionId\", \"GrantedBy\") VALUES ('role-editor', 'users.read', 'test'), ('role-manager', 'roles.manage', 'test'), ('role-disabled', 'users.read', 'test');");
                await ExecuteAsync(connection,
                    "UPDATE \"RoleSecurityProfiles\" SET \"IsDisabled\" = true WHERE \"RoleId\" = 'role-disabled';");
            }

            var service = new EffectivePermissionService(database);

            IReadOnlyList<string> admin = await service.GetEffectivePermissionsAsync("user-admin", Ct);
            Assert.True(AdminPermissions.AllIds.SetEquals(admin));

            IReadOnlyList<string> editor = await service.GetEffectivePermissionsAsync("user-editor", Ct);
            Assert.Equal([AdminPermissions.Users.Read], editor);

            IReadOnlyList<string> manager = await service.GetEffectivePermissionsAsync("user-manager", Ct);
            Assert.Contains(AdminPermissions.Roles.Manage, manager);
            Assert.Contains(AdminPermissions.Roles.Read, manager);
            Assert.Contains(AdminPermissions.Roles.Create, manager);
            Assert.Contains(AdminPermissions.Roles.Update, manager);
            Assert.Contains(AdminPermissions.Roles.Delete, manager);
            Assert.Contains(AdminPermissions.Roles.Restore, manager);
            Assert.DoesNotContain(AdminPermissions.Users.Read, manager);

            Assert.Empty(await service.GetEffectivePermissionsAsync("user-disabled", Ct));
            Assert.Empty(await service.GetEffectivePermissionsAsync("user-ordinary", Ct));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(rootConnectionString, databaseName);
        }
    }

    private static IdentityRole Role(string id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = Guid.NewGuid().ToString("N")
    };

    private static User User(string id) => new()
    {
        Id = id,
        UserName = $"{id}@paladinhub.test",
        NormalizedUserName = $"{id}@paladinhub.test".ToUpperInvariant(),
        Email = $"{id}@paladinhub.test",
        NormalizedEmail = $"{id}@paladinhub.test".ToUpperInvariant(),
        EmailConfirmed = true,
        FullName = id,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N")
    };

    private static async Task ExecuteUpgradeAsync(string connectionString)
    {
        Stream stream = typeof(AuthApiController).Assembly
            .GetManifestResourceStream("DatabaseUpgrades.RolesPermissions.sql")
            ?? throw new InvalidOperationException("Embedded roles/permissions upgrade was not found.");
        using var reader = new StreamReader(stream);
        string sql = await reader.ReadToEndAsync(Ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await ExecuteAsync(connection, sql);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<string> CreateDatabaseAsync(string rootConnectionString, string databaseName)
    {
        var test = new NpgsqlConnectionStringBuilder(rootConnectionString) { Database = databaseName };
        await using var connection = new NpgsqlConnection(rootConnectionString);
        await connection.OpenAsync(Ct);
        await ExecuteAsync(connection, $"CREATE DATABASE \"{databaseName}\";");
        return test.ConnectionString;
    }

    private static async Task DropDatabaseAsync(string rootConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(rootConnectionString);
        await connection.OpenAsync(Ct);
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);");
    }
}
