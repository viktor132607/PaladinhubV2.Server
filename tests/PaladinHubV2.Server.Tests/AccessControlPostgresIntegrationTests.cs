using Npgsql;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlPostgresIntegrationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RolesPermissionsUpgradeIsIdempotentAndPreservesExistingAdminMembership()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        string schema = "access_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);

        try
        {
            await ExecuteAsync(connection, $"""
                CREATE SCHEMA "{schema}";
                SET search_path TO "{schema}";

                CREATE TABLE "AspNetRoles" (
                    "Id" text PRIMARY KEY,
                    "Name" character varying(256) NULL,
                    "NormalizedName" character varying(256) NULL,
                    "ConcurrencyStamp" text NULL
                );

                CREATE TABLE "AspNetUsers" (
                    "Id" text PRIMARY KEY
                );

                CREATE TABLE "AspNetUserRoles" (
                    "UserId" text NOT NULL,
                    "RoleId" text NOT NULL,
                    PRIMARY KEY ("UserId", "RoleId"),
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                    FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
                );

                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName") VALUES
                    ('role-admin', 'Admin', 'ADMIN'),
                    ('role-editor', 'Editor', 'EDITOR');
                INSERT INTO "AspNetUsers" ("Id") VALUES ('admin-user');
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                    VALUES ('admin-user', 'role-admin');
                """);

            string upgradeSql = await ReadUpgradeSqlAsync();
            await ExecuteAsync(connection, upgradeSql);
            await ExecuteAsync(connection, upgradeSql);

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM "RoleSecurityProfiles";
                """, connection))
            {
                Assert.Equal(2L, (long)(await command.ExecuteScalarAsync(Ct))!);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT "IsSystem", "IsDisabled", "Version"
                FROM "RoleSecurityProfiles"
                WHERE "RoleId" = 'role-admin';
                """, connection))
            await using (var reader = await command.ExecuteReaderAsync(Ct))
            {
                Assert.True(await reader.ReadAsync(Ct));
                Assert.True(reader.GetBoolean(0));
                Assert.False(reader.GetBoolean(1));
                Assert.Equal(1, reader.GetInt32(2));
            }

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM "AspNetUserRoles"
                WHERE "UserId" = 'admin-user'
                  AND "RoleId" = 'role-admin';
                """, connection))
            {
                Assert.Equal(1L, (long)(await command.ExecuteScalarAsync(Ct))!);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND indexname IN (
                      'IX_RolePermissions_PermissionId',
                      'IX_RoleSecurityRevisions_RoleId_Version',
                      'IX_AccessControlAuditEntries_TargetRoleId_CreatedAtUtc',
                      'IX_AccessControlAuditEntries_TargetUserId_CreatedAtUtc');
                """, connection))
            {
                Assert.Equal(4L, (long)(await command.ExecuteScalarAsync(Ct))!);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = current_schema()
                  AND table_name = 'AccessControlAuditEntries';
                """, connection))
            {
                Assert.Equal(1L, (long)(await command.ExecuteScalarAsync(Ct))!);
            }

            PostgresException deleteError = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = new NpgsqlCommand("""
                    DELETE FROM "AspNetRoles" WHERE "Id" = 'role-admin';
                    """, connection);
                await command.ExecuteNonQueryAsync(Ct);
            });
            Assert.Equal("23503", deleteError.SqlState);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;",
                connection);
            await cleanup.ExecuteNonQueryAsync(Ct);
        }
    }

    [Fact]
    public async Task ConcurrentAdministratorRevocationsLeaveExactlyOneActiveAdministrator()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        string schema = "admin_race_" + Guid.NewGuid().ToString("N");
        await using var setup = new NpgsqlConnection(connectionString);
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
                VALUES ('role-admin', 'Admin', 'ADMIN', 'role-stamp');

                INSERT INTO "AspNetUsers" (
                    "Id", "UserName", "NormalizedUserName", "Email", "SecurityStamp", "LockoutEnabled")
                VALUES
                    ('admin-one', 'admin-one', 'ADMIN-ONE', 'one@example.com', 'stamp-one', true),
                    ('admin-two', 'admin-two', 'ADMIN-TWO', 'two@example.com', 'stamp-two', true);

                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES
                    ('admin-one', 'role-admin'),
                    ('admin-two', 'role-admin');
                """);

            await ExecuteAsync(setup, await ReadUpgradeSqlAsync());

            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            };
            var serviceOne = AccessControlAdminService.ForPostgres(builder.ConnectionString);
            var serviceTwo = AccessControlAdminService.ForPostgres(builder.ConnectionString);

            async Task<bool> AttemptAsync(AccessControlAdminService service, string userId, string actorId)
            {
                try
                {
                    await service.RevokeUserAsync(
                        "role-admin",
                        userId,
                        new AccessControlActor(actorId, actorId),
                        Ct);
                    return true;
                }
                catch (AccessControlAdminException exception) when (exception.Failure == AccessControlFailure.Conflict)
                {
                    return false;
                }
            }

            bool[] results = await Task.WhenAll(
                AttemptAsync(serviceOne, "admin-one", "operator-one"),
                AttemptAsync(serviceTwo, "admin-two", "operator-two"));

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);

            await using var verify = new NpgsqlConnection(builder.ConnectionString);
            await verify.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM "AspNetUserRoles"
                WHERE "RoleId" = 'role-admin';
                """, verify);
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync(Ct))!);
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
            ?? throw new InvalidOperationException(
                "Embedded roles/permissions upgrade was not found.");

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
