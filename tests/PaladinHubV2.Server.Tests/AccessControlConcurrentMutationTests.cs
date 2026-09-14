using Npgsql;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlConcurrentMutationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AssignmentAndRoleDeletionCannotBothSucceedConcurrently()
    {
        string? root = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Skip("Set SEO_POSTGRES_CONNECTION to run PostgreSQL integration tests.");
            return;
        }

        string schema = "access_mutation_" + Guid.NewGuid().ToString("N");
        await using var setup = new NpgsqlConnection(root);
        await setup.OpenAsync(Ct);

        try
        {
            await ExecuteAsync(setup, $"""
                CREATE SCHEMA "{schema}";
                SET search_path TO "{schema}";
                CREATE TABLE "AspNetRoles" (
                    "Id" text PRIMARY KEY,
                    "Name" character varying(256) NULL,
                    "NormalizedName" character varying(256) NULL,
                    "ConcurrencyStamp" text NULL
                );
                CREATE TABLE "AspNetUsers" (
                    "Id" text PRIMARY KEY,
                    "UserName" character varying(256) NULL,
                    "NormalizedUserName" character varying(256) NULL,
                    "Email" character varying(256) NULL,
                    "SecurityStamp" text NULL,
                    "LockoutEnabled" boolean NOT NULL DEFAULT false,
                    "LockoutEnd" timestamp with time zone NULL
                );
                CREATE TABLE "AspNetUserRoles" (
                    "UserId" text NOT NULL,
                    "RoleId" text NOT NULL,
                    PRIMARY KEY ("UserId", "RoleId"),
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                    FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES ('role-editor', 'Editor', 'EDITOR', 'role-stamp');
                INSERT INTO "AspNetUsers" (
                    "Id", "UserName", "NormalizedUserName", "Email", "SecurityStamp", "LockoutEnabled")
                VALUES ('user-one', 'user-one', 'USER-ONE', 'user-one@example.com', 'user-stamp', true);
                """);

            await ExecuteAsync(setup, await ReadUpgradeSqlAsync());

            var builder = new NpgsqlConnectionStringBuilder(root)
            {
                SearchPath = schema
            };
            AccessControlAdminService assignService =
                AccessControlAdminService.ForPostgres(builder.ConnectionString);
            AccessControlAdminService deleteService =
                AccessControlAdminService.ForPostgres(builder.ConnectionString);

            async Task<bool> AssignAsync()
            {
                try
                {
                    await assignService.AssignUserAsync(
                        "role-editor",
                        "user-one",
                        new AccessControlActor("operator-a", "Operator A"),
                        Ct);
                    return true;
                }
                catch (AccessControlAdminException)
                {
                    return false;
                }
            }

            async Task<bool> DeleteAsync()
            {
                try
                {
                    await deleteService.DeleteRoleAsync(
                        "role-editor",
                        1,
                        new AccessControlActor("operator-b", "Operator B"),
                        Ct);
                    return true;
                }
                catch (AccessControlAdminException)
                {
                    return false;
                }
            }

            bool[] results = await Task.WhenAll(AssignAsync(), DeleteAsync());
            Assert.Single(results, succeeded => succeeded);
            Assert.Single(results, succeeded => !succeeded);

            await using var verify = new NpgsqlConnection(builder.ConnectionString);
            await verify.OpenAsync(Ct);
            await using var roleCountCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"AspNetRoles\" WHERE \"Id\" = 'role-editor';",
                verify);
            long roleCount = (long)(await roleCountCommand.ExecuteScalarAsync(Ct))!;
            await using var membershipCountCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"AspNetUserRoles\" WHERE \"RoleId\" = 'role-editor' AND \"UserId\" = 'user-one';",
                verify);
            long membershipCount = (long)(await membershipCountCommand.ExecuteScalarAsync(Ct))!;

            Assert.Equal(1, roleCount); // Deletion preserves the role and revision history.
            var finalRole = await assignService.GetRoleAsync("role-editor", Ct);
            Assert.Equal(results[1], finalRole.IsDeleted);
            Assert.Equal(results[0] ? 1 : 0, membershipCount);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;",
                setup);
            await cleanup.ExecuteNonQueryAsync(Ct);
        }
    }

    private static async Task<string> ReadUpgradeSqlAsync()
    {
        Stream stream = typeof(SeoController).Assembly
            .GetManifestResourceStream("DatabaseUpgrades.RolesPermissions.sql")
            ?? throw new InvalidOperationException("Embedded roles/permissions upgrade was not found.");
        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            return await reader.ReadToEndAsync(Ct);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
