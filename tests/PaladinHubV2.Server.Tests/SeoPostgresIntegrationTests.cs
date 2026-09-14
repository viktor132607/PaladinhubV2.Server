using Npgsql;
using PaladinHubV2.Server.API.Controllers.Content;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoPostgresIntegrationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeoUpgradeUpdatesOldSchemaIdempotentlyAndGuardsMedia()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set SEO_POSTGRES_CONNECTION to run PostgreSQL integration tests.");
            return;
        }

        string schema = "seo_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);

        try
        {
            await ExecuteAsync(connection, $"""
                CREATE SCHEMA "{schema}";
                SET search_path TO "{schema}";

                CREATE TABLE "ContentPages" (
                    "Id" integer PRIMARY KEY
                );
                CREATE TABLE "SpellIcons" (
                    "Id" uuid PRIMARY KEY,
                    "IsArchived" boolean NOT NULL DEFAULT false,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );

                -- Reproduce the handoff/checkpoint-era SEO schema. The current
                -- upgrade must evolve it without dropping existing data or
                -- adding duplicate foreign keys.
                CREATE TABLE "SeoEntries" (
                    "Id" uuid PRIMARY KEY,
                    "PageId" integer NULL REFERENCES "ContentPages"("Id") ON DELETE RESTRICT,
                    "Path" varchar(2048) NOT NULL,
                    "Title" varchar(200) NOT NULL,
                    "Description" varchar(500) NOT NULL,
                    "CanonicalUrl" varchar(2048) NOT NULL,
                    "SocialTitle" varchar(200) NOT NULL,
                    "SocialDescription" varchar(500) NOT NULL,
                    "ImageUrl" varchar(2048) NOT NULL,
                    "Index" boolean NULL,
                    "Follow" boolean NULL,
                    "IsArchived" boolean NOT NULL DEFAULT false,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "Version" integer NOT NULL DEFAULT 1
                );
                CREATE INDEX "IX_SeoEntries_PageId" ON "SeoEntries"("PageId");

                CREATE TABLE "SeoRevisions" (
                    "Id" uuid PRIMARY KEY,
                    "EntryId" uuid NOT NULL REFERENCES "SeoEntries"("Id") ON DELETE RESTRICT,
                    "Version" integer NOT NULL,
                    "Action" text NOT NULL,
                    "Actor" text NOT NULL,
                    "Snapshot" text NOT NULL,
                    "CreatedAtUtc" timestamptz NOT NULL
                );
                CREATE UNIQUE INDEX "IX_SeoRevisions_EntryId_Version"
                    ON "SeoRevisions"("EntryId", "Version");
                """);

            string upgradeSql = await ReadUpgradeSqlAsync();
            await ExecuteAsync(connection, upgradeSql);
            await ExecuteAsync(connection, upgradeSql);

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND indexname IN (
                      'IX_SeoEntries_PageId',
                      'IX_SeoEntries_SocialImageMediaId',
                      'UX_SeoEntries_Active_PageId',
                      'UX_SeoEntries_Active_StaticPath',
                      'IX_SeoRevisions_EntryId_Version');
                """, connection))
            {
                long indexCount = (long)(await command.ExecuteScalarAsync(Ct))!;
                Assert.Equal(5, indexCount);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE contype = 'f'
                  AND conrelid = '"SeoEntries"'::regclass;
                """, connection))
            {
                long foreignKeys = (long)(await command.ExecuteScalarAsync(Ct))!;
                Assert.Equal(2, foreignKeys);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE contype = 'f'
                  AND conrelid = '"SeoRevisions"'::regclass;
                """, connection))
            {
                long foreignKeys = (long)(await command.ExecuteScalarAsync(Ct))!;
                Assert.Equal(1, foreignKeys);
            }

            Guid mediaId = Guid.NewGuid();
            Guid seoId = Guid.NewGuid();
            await using (var command = new NpgsqlCommand("""
                INSERT INTO "SpellIcons" ("Id") VALUES (@mediaId);
                INSERT INTO "SeoEntries" (
                    "Id",
                    "Path",
                    "Title",
                    "Description",
                    "CanonicalUrl",
                    "SocialTitle",
                    "SocialDescription",
                    "SocialImageMediaId",
                    "ImageUrl")
                VALUES (
                    @seoId,
                    '/',
                    'Title',
                    'Description',
                    '',
                    'Social title',
                    'Social description',
                    @mediaId,
                    '');
                """, connection))
            {
                command.Parameters.AddWithValue("mediaId", mediaId);
                command.Parameters.AddWithValue("seoId", seoId);
                await command.ExecuteNonQueryAsync(Ct);
            }

            PostgresException error = await Assert.ThrowsAsync<PostgresException>(
                async () =>
                {
                    await using var command = new NpgsqlCommand("""
                        UPDATE "SpellIcons"
                        SET "IsArchived" = true
                        WHERE "Id" = @mediaId;
                        """, connection);
                    command.Parameters.AddWithValue("mediaId", mediaId);
                    await command.ExecuteNonQueryAsync(Ct);
                });

            Assert.Equal("23503", error.SqlState);
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
    public async Task SeoMutationAdvisoryLockSerializesConcurrentTransactions()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set SEO_POSTGRES_CONNECTION to run PostgreSQL integration tests.");
            return;
        }

        await using var firstConnection = new NpgsqlConnection(connectionString);
        await using var secondConnection = new NpgsqlConnection(connectionString);
        await firstConnection.OpenAsync(Ct);
        await secondConnection.OpenAsync(Ct);

        await using NpgsqlTransaction firstTransaction =
            await firstConnection.BeginTransactionAsync(Ct);
        await using NpgsqlTransaction secondTransaction =
            await secondConnection.BeginTransactionAsync(Ct);

        await using (var takeLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(8820414);",
            firstConnection,
            firstTransaction))
        {
            await takeLock.ExecuteNonQueryAsync(Ct);
        }

        await using (var tryLock = new NpgsqlCommand(
            "SELECT pg_try_advisory_xact_lock(8820414);",
            secondConnection,
            secondTransaction))
        {
            bool acquired = (bool)(await tryLock.ExecuteScalarAsync(Ct))!;
            Assert.False(acquired);
        }

        await firstTransaction.CommitAsync(Ct);

        await using (var tryLock = new NpgsqlCommand(
            "SELECT pg_try_advisory_xact_lock(8820414);",
            secondConnection,
            secondTransaction))
        {
            bool acquired = (bool)(await tryLock.ExecuteScalarAsync(Ct))!;
            Assert.True(acquired);
        }

        await secondTransaction.CommitAsync(Ct);
    }

    private static async Task<string> ReadUpgradeSqlAsync()
    {
        Stream stream = typeof(SeoController).Assembly
            .GetManifestResourceStream("DatabaseUpgrades.Seo.sql")
            ?? throw new InvalidOperationException("Embedded SEO upgrade was not found.");

        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            return await reader.ReadToEndAsync(Ct);
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
