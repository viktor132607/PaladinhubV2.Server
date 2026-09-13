using Npgsql;
using PaladinHubV2.Server.API.Controllers.Content;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlUpgradeCleanupTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpgradeRemovesRetiredPermissionGrantsWithoutChangingIdentityMemberships()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        string schema = "permission_cleanup_" + Guid.NewGuid().ToString("N");
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

                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName")
                VALUES ('role-admin', 'Admin', 'ADMIN'), ('role-editor', 'Editor', 'EDITOR');
                INSERT INTO "AspNetUsers" ("Id") VALUES ('admin-user'), ('editor-user');
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES
                    ('admin-user', 'role-admin'),
                    ('editor-user', 'role-editor');
                """);

            string upgrade = await ReadUpgradeSqlAsync();
            await ExecuteAsync(connection, upgrade);
            await ExecuteAsync(connection, """
                INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedBy") VALUES
                    ('role-editor', 'users.create', 'legacy'),
                    ('role-editor', 'media.archive', 'legacy'),
                    ('role-editor', 'pages.read', 'legacy');
                """);

            await ExecuteAsync(connection, upgrade);
            await ExecuteAsync(connection, upgrade);

            await using (var retired = new NpgsqlCommand("""
                SELECT COUNT(*) FROM "RolePermissions"
                WHERE "PermissionId" IN ('users.create', 'media.archive');
                """, connection))
            {
                Assert.Equal(0L, (long)(await retired.ExecuteScalarAsync(Ct))!);
            }

            await using (var supported = new NpgsqlCommand("""
                SELECT COUNT(*) FROM "RolePermissions"
                WHERE "RoleId" = 'role-editor' AND "PermissionId" = 'pages.read';
                """, connection))
            {
                Assert.Equal(1L, (long)(await supported.ExecuteScalarAsync(Ct))!);
            }

            await using (var memberships = new NpgsqlCommand("""
                SELECT COUNT(*) FROM "AspNetUserRoles";
                """, connection))
            {
                Assert.Equal(2L, (long)(await memberships.ExecuteScalarAsync(Ct))!);
            }
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
