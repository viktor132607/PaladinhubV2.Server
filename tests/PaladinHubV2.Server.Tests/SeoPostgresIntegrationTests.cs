using Npgsql;
using PaladinHubV2.Server.API.Controllers.Content;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoPostgresIntegrationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeoUpgradeIsIdempotentAndPreventsReferencedMediaDeactivation()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
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

            Guid mediaId = Guid.NewGuid();
            Guid seoId = Guid.NewGuid();
            await using (var command = new NpgsqlCommand("""
                INSERT INTO "SpellIcons" ("Id") VALUES (@mediaId);
                INSERT INTO "SeoEntries" ("Id", "SocialImageMediaId")
                VALUES (@seoId, @mediaId);
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
        await using Stream? stream = typeof(SeoController).Assembly
            .GetManifestResourceStream("DatabaseUpgrades.Seo.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(Ct);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
