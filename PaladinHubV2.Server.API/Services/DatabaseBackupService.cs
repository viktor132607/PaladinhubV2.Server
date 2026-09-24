using Microsoft.Extensions.Logging;

namespace PaladinHubV2.Server.API.Services;

public sealed class DatabaseBackupService
{
    private readonly IPostgresToolRunner _tools;
    private readonly IDatabaseBackupFileStore _files;
    private readonly IPgDumpArchiveValidator _validator;
    private readonly IDatabasePoolManager _pools;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly SemaphoreSlim _operationLock =
        new(1, 1);

    public DatabaseBackupService(
        string connectionString,
        ILogger<DatabaseBackupService> logger)
        : this(
            new PostgresToolRunner(
                connectionString,
                new SystemExternalProcessExecutor(),
                logger),
            new PhysicalDatabaseBackupFileStore(),
            new PgDumpArchiveValidator(),
            new NpgsqlDatabasePoolManager(),
            TimeProvider.System,
            logger)
    {
    }

    public DatabaseBackupService(
        IPostgresToolRunner tools,
        IDatabaseBackupFileStore files,
        IPgDumpArchiveValidator validator,
        IDatabasePoolManager pools,
        TimeProvider timeProvider,
        ILogger<DatabaseBackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(pools);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _tools = tools;
        _files = files;
        _validator = validator;
        _pools = pools;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DatabaseBackupArtifact>
        CreateBackupAsync(
            CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(
            cancellationToken);

        string? backupPath = null;

        try
        {
            backupPath =
                _files.CreateTemporaryPath("dump");

            await _tools.RunAsync(
                "pg_dump",
                [
                    "--format=custom",
                    "--compress=9",
                    "--file",
                    backupPath
                ],
                "create the database backup",
                cancellationToken);

            await _validator.ValidateAsync(
                backupPath,
                cancellationToken);

            long backupSize =
                _files.GetLength(backupPath);

            if (backupSize == 0)
            {
                throw new InvalidOperationException(
                    "PostgreSQL reported a successful backup, but the generated archive is empty.");
            }

            string downloadName =
                $"paladinhub-full-database-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}Z.dump";

            _logger.LogInformation(
                "Full PostgreSQL backup created successfully ({BackupSize} bytes).",
                backupSize);

            return new DatabaseBackupArtifact(
                backupPath,
                downloadName);
        }
        catch
        {
            if (backupPath is not null)
            {
                _files.TryDelete(backupPath);
            }

            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RestoreBackupAsync(
        Stream archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);

        await _operationLock.WaitAsync(
            cancellationToken);

        string? uploadedPath = null;
        string? sqlPath = null;
        string? resetPath = null;

        try
        {
            uploadedPath =
                _files.CreateTemporaryPath("dump");

            await _files.CopyToNewFileAsync(
                archive,
                uploadedPath,
                cancellationToken);

            long backupSize =
                _files.GetLength(uploadedPath);

            if (backupSize == 0)
            {
                throw new InvalidDataException(
                    "The uploaded backup archive is empty.");
            }

            await _validator.ValidateAsync(
                uploadedPath,
                cancellationToken);

            try
            {
                await _tools.RunAsync(
                    "pg_restore",
                    ["--list", uploadedPath],
                    "validate the uploaded database backup",
                    cancellationToken);
            }
            catch (PostgresToolException ex)
            {
                throw new InvalidDataException(
                    "The uploaded file is not a valid PostgreSQL custom-format backup archive.",
                    ex);
            }

            sqlPath =
                _files.CreateTemporaryPath("sql");
            resetPath =
                _files.CreateTemporaryPath("sql");

            await _tools.RunAsync(
                "pg_restore",
                [
                    "--clean",
                    "--if-exists",
                    "--no-owner",
                    "--no-privileges",
                    "--file",
                    sqlPath,
                    uploadedPath
                ],
                "read the complete database archive",
                cancellationToken);

            await _files.WriteAllTextAsync(
                resetPath,
                ResetDatabaseSql,
                cancellationToken);

            _pools.Clear();

            try
            {
                await _tools.RunAsync(
                    "psql",
                    [
                        "--no-psqlrc",
                        "--no-password",
                        "--single-transaction",
                        "--set=ON_ERROR_STOP=on",
                        "--file",
                        resetPath,
                        "--file",
                        sqlPath
                    ],
                    "restore the database backup",
                    cancellationToken);
            }
            finally
            {
                _pools.Clear();
            }

            _logger.LogWarning(
                "Full PostgreSQL database restore completed successfully from an uploaded archive ({BackupSize} bytes).",
                backupSize);
        }
        finally
        {
            if (uploadedPath is not null)
            {
                _files.TryDelete(uploadedPath);
            }

            if (sqlPath is not null)
            {
                _files.TryDelete(sqlPath);
            }

            if (resetPath is not null)
            {
                _files.TryDelete(resetPath);
            }

            _operationLock.Release();
        }
    }

    internal const string ResetDatabaseSql = """
        SET LOCAL lock_timeout = '30s';
        SELECT pg_advisory_xact_lock(723480193);
        DO $reset$
        DECLARE item record;
        BEGIN
            FOR item IN SELECT extname FROM pg_extension WHERE extname <> 'plpgsql'
            LOOP EXECUTE format('DROP EXTENSION %I CASCADE', item.extname); END LOOP;
            FOR item IN SELECT nspname FROM pg_namespace
                WHERE nspname <> 'information_schema' AND nspname !~ '^pg_'
            LOOP EXECUTE format('DROP SCHEMA %I CASCADE', item.nspname); END LOOP;
            PERFORM lo_unlink(oid) FROM pg_largeobject_metadata;
        END $reset$;
        CREATE SCHEMA public;
        GRANT USAGE ON SCHEMA public TO PUBLIC;
        """;
}
