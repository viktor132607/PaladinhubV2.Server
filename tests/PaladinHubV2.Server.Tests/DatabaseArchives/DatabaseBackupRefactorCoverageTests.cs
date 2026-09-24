using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaladinHubV2.Server.API.Services;

namespace PaladinHubV2.Server.Tests.DatabaseArchives;

public sealed class DatabaseBackupRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateBackupBuildsArtifactAndKeepsSuccessfulFile()
    {
        var tools = new Mock<IPostgresToolRunner>();
        var files = new Mock<IDatabaseBackupFileStore>();
        var validator = new Mock<IPgDumpArchiveValidator>();
        var pools = new Mock<IDatabasePoolManager>();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(
                2026, 9, 25, 0, 1, 2,
                TimeSpan.Zero));

        files.Setup(x =>
                x.CreateTemporaryPath("dump"))
            .Returns("/tmp/backup.dump");
        files.Setup(x =>
                x.GetLength("/tmp/backup.dump"))
            .Returns(1234);

        var service = new DatabaseBackupService(
            tools.Object,
            files.Object,
            validator.Object,
            pools.Object,
            clock,
            NullLogger<DatabaseBackupService>.Instance);

        DatabaseBackupArtifact artifact =
            await service.CreateBackupAsync(Ct);

        Assert.Equal(
            "/tmp/backup.dump",
            artifact.FilePath);
        Assert.Equal(
            "paladinhub-full-database-20260925-000102Z.dump",
            artifact.FileName);

        tools.Verify(x => x.RunAsync(
            "pg_dump",
            It.Is<IReadOnlyCollection<string>>(
                args =>
                    args.Contains("--format=custom") &&
                    args.Contains("--compress=9") &&
                    args.Contains("/tmp/backup.dump")),
            "create the database backup",
            Ct),
            Times.Once);

        validator.Verify(x => x.ValidateAsync(
            "/tmp/backup.dump",
            Ct),
            Times.Once);

        files.Verify(x =>
                x.TryDelete(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBackupDeletesTemporaryFileOnFailureOrEmptyOutput()
    {
        var tools = new Mock<IPostgresToolRunner>();
        var files = new Mock<IDatabaseBackupFileStore>();
        var validator = new Mock<IPgDumpArchiveValidator>();

        files.Setup(x =>
                x.CreateTemporaryPath("dump"))
            .Returns("/tmp/failure.dump");

        tools.SetupSequence(x => x.RunAsync(
                "pg_dump",
                It.IsAny<IReadOnlyCollection<string>>(),
                "create the database backup",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Returns(Task.CompletedTask);

        DatabaseBackupService service =
            Service(
                tools.Object,
                files.Object,
                validator.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateBackupAsync(Ct));

        files.Verify(x =>
                x.TryDelete("/tmp/failure.dump"),
            Times.Once);

        files.Setup(x =>
                x.GetLength("/tmp/failure.dump"))
            .Returns(0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateBackupAsync(Ct));

        files.Verify(x =>
                x.TryDelete("/tmp/failure.dump"),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RestoreRejectsNullEmptyAndInvalidToolValidation()
    {
        var tools = new Mock<IPostgresToolRunner>();
        var files = new Mock<IDatabaseBackupFileStore>();
        var validator = new Mock<IPgDumpArchiveValidator>();

        DatabaseBackupService service =
            Service(
                tools.Object,
                files.Object,
                validator.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RestoreBackupAsync(
                null!,
                Ct));

        files.Setup(x =>
                x.CreateTemporaryPath("dump"))
            .Returns("/tmp/upload.dump");
        files.Setup(x =>
                x.GetLength("/tmp/upload.dump"))
            .Returns(0);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RestoreBackupAsync(
                new MemoryStream([1]),
                Ct));

        files.Verify(x =>
                x.TryDelete("/tmp/upload.dump"),
            Times.Once);

        files.Setup(x =>
                x.GetLength("/tmp/upload.dump"))
            .Returns(10);

        tools.Setup(x => x.RunAsync(
                "pg_restore",
                It.Is<IReadOnlyCollection<string>>(
                    args => args.Contains("--list")),
                "validate the uploaded database backup",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new PostgresToolException(
                    "pg_restore",
                    "validate",
                    1,
                    "bad archive"));

        InvalidDataException invalid =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => service.RestoreBackupAsync(
                    new MemoryStream([1]),
                    Ct));

        Assert.IsType<PostgresToolException>(
            invalid.InnerException);
    }

    [Fact]
    public async Task RestoreMaterializesAndExecutesDatabaseReset()
    {
        var tools = new Mock<IPostgresToolRunner>();
        var files = new Mock<IDatabaseBackupFileStore>();
        var validator = new Mock<IPgDumpArchiveValidator>();
        var pools = new Mock<IDatabasePoolManager>();

        files.SetupSequence(x =>
                x.CreateTemporaryPath(
                    It.IsAny<string>()))
            .Returns("/tmp/upload.dump")
            .Returns("/tmp/archive.sql")
            .Returns("/tmp/reset.sql");

        files.Setup(x =>
                x.GetLength("/tmp/upload.dump"))
            .Returns(456);

        DatabaseBackupService service =
            Service(
                tools.Object,
                files.Object,
                validator.Object,
                pools.Object);

        await service.RestoreBackupAsync(
            new MemoryStream([1, 2, 3]),
            Ct);

        files.Verify(x => x.CopyToNewFileAsync(
            It.IsAny<Stream>(),
            "/tmp/upload.dump",
            Ct),
            Times.Once);

        validator.Verify(x => x.ValidateAsync(
            "/tmp/upload.dump",
            Ct),
            Times.Once);

        tools.Verify(x => x.RunAsync(
            "pg_restore",
            It.Is<IReadOnlyCollection<string>>(
                args =>
                    args.Contains("--clean") &&
                    args.Contains("--if-exists") &&
                    args.Contains("/tmp/archive.sql") &&
                    args.Contains("/tmp/upload.dump")),
            "read the complete database archive",
            Ct),
            Times.Once);

        files.Verify(x => x.WriteAllTextAsync(
            "/tmp/reset.sql",
            It.Is<string>(sql =>
                sql.Contains(
                    "pg_advisory_xact_lock") &&
                sql.Contains(
                    "DROP SCHEMA")),
            Ct),
            Times.Once);

        tools.Verify(x => x.RunAsync(
            "psql",
            It.Is<IReadOnlyCollection<string>>(
                args =>
                    args.Contains("--single-transaction") &&
                    args.Contains("/tmp/reset.sql") &&
                    args.Contains("/tmp/archive.sql")),
            "restore the database backup",
            Ct),
            Times.Once);

        pools.Verify(x => x.Clear(),
            Times.Exactly(2));

        files.Verify(x =>
                x.TryDelete("/tmp/upload.dump"),
            Times.Once);
        files.Verify(x =>
                x.TryDelete("/tmp/archive.sql"),
            Times.Once);
        files.Verify(x =>
                x.TryDelete("/tmp/reset.sql"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreAlwaysClearsPoolsAndTemporaryFilesWhenPsqlFails()
    {
        var tools = new Mock<IPostgresToolRunner>();
        var files = new Mock<IDatabaseBackupFileStore>();
        var validator = new Mock<IPgDumpArchiveValidator>();
        var pools = new Mock<IDatabasePoolManager>();

        files.SetupSequence(x =>
                x.CreateTemporaryPath(
                    It.IsAny<string>()))
            .Returns("/tmp/upload.dump")
            .Returns("/tmp/archive.sql")
            .Returns("/tmp/reset.sql");

        files.Setup(x =>
                x.GetLength("/tmp/upload.dump"))
            .Returns(100);

        tools.Setup(x => x.RunAsync(
                "psql",
                It.IsAny<IReadOnlyCollection<string>>(),
                "restore the database backup",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "restore failed"));

        DatabaseBackupService service =
            Service(
                tools.Object,
                files.Object,
                validator.Object,
                pools.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreBackupAsync(
                new MemoryStream([1]),
                Ct));

        pools.Verify(x => x.Clear(),
            Times.Exactly(2));

        files.Verify(x =>
                x.TryDelete(It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ToolRunnerBuildsPostgresEnvironmentAndHandlesResults()
    {
        var executor =
            new CapturingProcessExecutor(
                new ProcessExecutionResult(
                    0,
                    " notice "));

        var runner = new PostgresToolRunner(
            "Host=db.example;Port=6543;Database=paladin;Username=admin;Password=secret;SSL Mode=VerifyFull",
            executor,
            NullLogger.Instance);

        await runner.RunAsync(
            "pg_dump",
            ["--format=custom", "paladin"],
            "backup",
            Ct);

        ProcessStartInfo start =
            Assert.IsType<ProcessStartInfo>(
                executor.StartInfo);

        Assert.Equal("pg_dump", start.FileName);
        Assert.Equal(
            ["--format=custom", "paladin"],
            start.ArgumentList);
        Assert.Equal(
            "db.example",
            start.Environment["PGHOST"]);
        Assert.Equal(
            "6543",
            start.Environment["PGPORT"]);
        Assert.Equal(
            "paladin",
            start.Environment["PGDATABASE"]);
        Assert.Equal(
            "admin",
            start.Environment["PGUSER"]);
        Assert.Equal(
            "secret",
            start.Environment["PGPASSWORD"]);
        Assert.Equal(
            "verify-full",
            start.Environment["PGSSLMODE"]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.FromResult(
                new PostgresToolRunner(
                    " ",
                    executor,
                    NullLogger.Instance)));

        Assert.Throws<ArgumentNullException>(
            () => new PostgresToolRunner(
                "Host=x;Database=x;Username=x",
                null!,
                NullLogger.Instance));

        Assert.Throws<ArgumentNullException>(
            () => new PostgresToolRunner(
                "Host=x;Database=x;Username=x",
                executor,
                null!));

        Assert.Throws<InvalidOperationException>(
            () => new PostgresToolRunner(
                "Database=x;Username=x",
                executor,
                NullLogger.Instance));
    }

    [Fact]
    public async Task ToolRunnerCoversSslModesAndPostgresFailures()
    {
        foreach (var (ssl, expected) in new[]
        {
            ("VerifyCA", "verify-ca"),
            ("Require", "require")
        })
        {
            var executor =
                new CapturingProcessExecutor(
                    new ProcessExecutionResult(
                        0,
                        string.Empty));

            var runner = new PostgresToolRunner(
                $"Host=x;Database=x;Username=x;SSL Mode={ssl}",
                executor,
                NullLogger.Instance);

            await runner.RunAsync(
                "pg_restore",
                [],
                "operation",
                Ct);

            Assert.Equal(
                expected,
                executor.StartInfo!
                    .Environment["PGSSLMODE"]);
            Assert.False(
                executor.StartInfo!
                    .Environment.ContainsKey("PGPASSWORD"));
        }

        var blankErrorExecutor =
            new CapturingProcessExecutor(
                new ProcessExecutionResult(
                    4,
                    " "));

        var failed = new PostgresToolRunner(
            "Host=x;Database=x;Username=x",
            blankErrorExecutor,
            NullLogger.Instance);

        PostgresToolException blank =
            await Assert.ThrowsAsync<PostgresToolException>(
                () => failed.RunAsync(
                    "pg_restore",
                    [],
                    "read archive",
                    Ct));

        Assert.Contains(
            "No error details were returned",
            blank.Message);

        var detailedExecutor =
            new CapturingProcessExecutor(
                new ProcessExecutionResult(
                    2,
                    "broken"));

        var detailed = new PostgresToolRunner(
            "Host=x;Database=x;Username=x",
            detailedExecutor,
            NullLogger.Instance);

        PostgresToolException error =
            await Assert.ThrowsAsync<PostgresToolException>(
                () => detailed.RunAsync(
                    "psql",
                    [],
                    "restore",
                    Ct));

        Assert.Contains("broken", error.Message);
    }

    [Fact]
    public async Task PhysicalFileStoreAndDumpValidatorCoverFilesystemBoundary()
    {
        var store =
            new PhysicalDatabaseBackupFileStore();
        var validator =
            new PgDumpArchiveValidator();

        string sourcePath =
            store.CreateTemporaryPath("source");
        string copyPath =
            store.CreateTemporaryPath("dump");
        string textPath =
            store.CreateTemporaryPath("sql");
        string shortPath =
            store.CreateTemporaryPath("dump");

        try
        {
            await File.WriteAllBytesAsync(
                sourcePath,
                Encoding.ASCII
                    .GetBytes("PGDMPpayload"),
                Ct);

            await using FileStream source =
                File.OpenRead(sourcePath);

            await store.CopyToNewFileAsync(
                source,
                copyPath,
                Ct);

            Assert.True(
                store.GetLength(copyPath) > 5);

            await validator.ValidateAsync(
                copyPath,
                Ct);

            await store.WriteAllTextAsync(
                textPath,
                "select 1;",
                Ct);

            Assert.True(
                store.GetLength(textPath) > 0);

            Assert.Equal(
                0,
                store.GetLength(
                    textPath + ".missing"));

            await File.WriteAllBytesAsync(
                shortPath,
                Encoding.ASCII.GetBytes("PG"),
                Ct);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => validator.ValidateAsync(
                    shortPath,
                    Ct));

            await File.WriteAllBytesAsync(
                shortPath,
                Encoding.ASCII.GetBytes("XXXXX"),
                Ct);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => validator.ValidateAsync(
                    shortPath,
                    Ct));
        }
        finally
        {
            store.TryDelete(sourcePath);
            store.TryDelete(copyPath);
            store.TryDelete(textPath);
            store.TryDelete(shortPath);
            store.TryDelete(
                textPath + ".missing");
        }

        Assert.Equal(0, store.GetLength(copyPath));
    }

    [Fact]
    public void PoolManagerCanClearNpgsqlPools()
    {
        new NpgsqlDatabasePoolManager().Clear();
    }

    [Fact]
    public void DependencyConstructorRejectsNullCollaborators()
    {
        var tools = new Mock<IPostgresToolRunner>().Object;
        var files = new Mock<IDatabaseBackupFileStore>().Object;
        var validator = new Mock<IPgDumpArchiveValidator>().Object;
        var pools = new Mock<IDatabasePoolManager>().Object;
        var clock = TimeProvider.System;
        var logger =
            NullLogger<DatabaseBackupService>.Instance;

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                null!,
                files,
                validator,
                pools,
                clock,
                logger));

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                tools,
                null!,
                validator,
                pools,
                clock,
                logger));

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                tools,
                files,
                null!,
                pools,
                clock,
                logger));

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                tools,
                files,
                validator,
                null!,
                clock,
                logger));

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                tools,
                files,
                validator,
                pools,
                null!,
                logger));

        Assert.Throws<ArgumentNullException>(
            () => new DatabaseBackupService(
                tools,
                files,
                validator,
                pools,
                clock,
                null!));
    }

    private static DatabaseBackupService Service(
        IPostgresToolRunner tools,
        IDatabaseBackupFileStore files,
        IPgDumpArchiveValidator validator,
        IDatabasePoolManager? pools = null)
    {
        return new DatabaseBackupService(
            tools,
            files,
            validator,
            pools ??
                new Mock<IDatabasePoolManager>()
                    .Object,
            TimeProvider.System,
            NullLogger<DatabaseBackupService>.Instance);
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }

    private sealed class CapturingProcessExecutor(
        ProcessExecutionResult result)
        : IExternalProcessExecutor
    {
        public ProcessStartInfo? StartInfo { get; private set; }

        public Task<ProcessExecutionResult> ExecuteAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            StartInfo = startInfo;
            return Task.FromResult(result);
        }
    }
}
