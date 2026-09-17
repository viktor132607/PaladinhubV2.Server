using System.Reflection;
using System.Text;
using PaladinHubV2.Server.API.Controllers;
using PaladinHubV2.Server.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace PaladinHubV2.Server.Tests.DatabaseArchives;

public sealed class DatabaseBackupTests
{
    private static DatabaseBackupService Service(string connection = "Host=localhost;Database=unused;Username=unused") =>
        new(connection, NullLogger<DatabaseBackupService>.Instance);

    [Fact]
    public void EndpointsRequireAdmin() => Assert.Equal("Admin",
        typeof(DatabaseBackupController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);

    [Fact]
    public async Task RestoreRequiresExplicitConfirmation()
    {
        var controller = new DatabaseBackupController(Service(), NullLogger<DatabaseBackupController>.Instance);
        Assert.IsType<BadRequestObjectResult>(await controller.Restore(null, null, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Restore(null, "RESTORE", default));
    }

    [Fact]
    public async Task RejectsInvalidArchiveBeforeConnecting()
    {
        await using var archive = new MemoryStream(Encoding.UTF8.GetBytes("not a PostgreSQL archive"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Service().RestoreBackupAsync(archive));
    }

    [Fact]
    public async Task FullRoundTripRemovesNewObjectsAndFailedRestoreRollsBack()
    {
        var configured = Environment.GetEnvironmentVariable("BACKUP_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrEmpty(configured), "BACKUP_TEST_CONNECTION must point to an isolated test PostgreSQL server.");
        var dbName = "backup_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(configured);
        await admin.OpenAsync();
        await Execute(admin, $"CREATE DATABASE {dbName}");
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = dbName, Pooling = false };
        string? backupPath = null;
        string? failurePath = null;
        try
        {
            await using var db = new NpgsqlConnection(builder.ConnectionString);
            await db.OpenAsync();
            await Execute(db, """
                CREATE TABLE public.parents(id serial PRIMARY KEY, name text);
                INSERT INTO public.parents(name) VALUES ('original'), ('second');
                CREATE SCHEMA archive_data;
                CREATE TABLE archive_data.children(id serial PRIMARY KEY, parent_id int REFERENCES public.parents(id), value bytea);
                INSERT INTO archive_data.children(parent_id,value) VALUES (1,decode('deadbeef','hex'));
                CREATE VIEW archive_data.names AS SELECT name FROM public.parents;
                SELECT lo_from_bytea(654321,decode('cafe','hex'));
                """);
            var service = Service(builder.ConnectionString);
            var backup = await service.CreateBackupAsync();
            backupPath = backup.FilePath;
            await Execute(db, """
                UPDATE public.parents SET name='changed';
                INSERT INTO public.parents(name) VALUES ('new');
                CREATE TABLE public.new_table(id int);
                CREATE SCHEMA new_schema;
                CREATE TABLE new_schema.new_table(id int);
                SELECT lo_from_bytea(654322,decode('abcd','hex'));
                """);
            await using (var stream = File.OpenRead(backupPath)) await service.RestoreBackupAsync(stream);
            Assert.Equal("original,second", await Scalar(db, "SELECT string_agg(name,',' ORDER BY id) FROM public.parents"));
            Assert.Equal("deadbeef", await Scalar(db, "SELECT encode(value,'hex') FROM archive_data.children"));
            Assert.Equal("original,second", await Scalar(db, "SELECT string_agg(name,',' ORDER BY name) FROM archive_data.names"));
            Assert.Equal("True", await Scalar(db, "SELECT to_regclass('public.new_table') IS NULL"));
            Assert.Equal("0", await Scalar(db, "SELECT count(*) FROM pg_namespace WHERE nspname='new_schema'"));
            Assert.Equal("cafe", await Scalar(db, "SELECT encode(lo_get(654321),'hex')"));
            Assert.Equal("0", await Scalar(db, "SELECT count(*) FROM pg_largeobject_metadata WHERE oid=654322"));
            Assert.Equal("3", await Scalar(db, "INSERT INTO public.parents(name) VALUES ('after') RETURNING id"));

            // Produce a valid archive whose COPY fails a CHECK constraint at restore time.
            await Execute(db, """
                CREATE FUNCTION public.restore_probe() RETURNS boolean LANGUAGE plpgsql AS $$
                BEGIN RETURN coalesce(current_setting('app.backup_probe',true),'')='allow'; END $$;
                CREATE TABLE public.restore_failure(id int CHECK(public.restore_probe()));
                SET app.backup_probe='allow';
                INSERT INTO public.restore_failure VALUES (1);
                """);
            var failing = await service.CreateBackupAsync();
            failurePath = failing.FilePath;
            await Execute(db, "DROP TABLE public.restore_failure; DROP FUNCTION public.restore_probe(); UPDATE public.parents SET name='keep' WHERE id=1;");
            await using (var stream = File.OpenRead(failurePath))
                await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.RestoreBackupAsync(stream));
            Assert.Equal("keep", await Scalar(db, "SELECT name FROM public.parents WHERE id=1"));
            Assert.Equal("True", await Scalar(db, "SELECT to_regclass('public.restore_failure') IS NULL"));
        }
        finally
        {
            if (backupPath is not null) File.Delete(backupPath);
            if (failurePath is not null) File.Delete(failurePath);
            await Execute(admin, $"DROP DATABASE {dbName} WITH (FORCE)");
        }
    }

    private static async Task Execute(NpgsqlConnection connection, string sql) =>
        await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
    private static async Task<string?> Scalar(NpgsqlConnection connection, string sql) =>
        (await new NpgsqlCommand(sql, connection).ExecuteScalarAsync())?.ToString();
}

