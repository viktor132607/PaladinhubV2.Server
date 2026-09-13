using Npgsql;
using PaladinHubV2.Server.Data;

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
                      'IX_RoleSecurityRevisions_RoleId_Version');
                """, connection))
            {
                Assert.Equal(2L, (long)(await command.ExecuteScalarAsync(Ct))!);
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

    private static async Task<string> ReadUpgradeSqlAsync()
    {
        Stream stream = typeof(AccessControlDbContext).Assembly
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
