using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Npgsql;

namespace PaladinHubV2.Server.API.Services;

public sealed record DatabaseBackupArtifact(string FilePath, string FileName);

public sealed class DatabaseBackupService
{
    private const int CopyBufferSize = 128 * 1024;
    private static readonly byte[] PgDumpMagic = Encoding.ASCII.GetBytes("PGDMP");

    private readonly NpgsqlConnectionStringBuilder connection;
    private readonly ILogger<DatabaseBackupService> logger;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public DatabaseBackupService(
        string connectionString,
        ILogger<DatabaseBackupService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        connection = new NpgsqlConnectionStringBuilder(connectionString);
        this.logger = logger;

        if (string.IsNullOrWhiteSpace(connection.Host) ||
            string.IsNullOrWhiteSpace(connection.Database) ||
            string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException(
                "Database backup requires PostgreSQL host, database, and username configuration.");
        }
    }

    public async Task<DatabaseBackupArtifact> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        string? backupPath = null;

        try
        {
            backupPath = CreateTemporaryPath("dump");

            await RunPostgresToolAsync(
                "pg_dump",
                [
                    "--format=custom",
                    "--compress=9",
                    "--file",
                    backupPath,
                    connection.Database!
                ],
                "create the database backup",
                cancellationToken);

            await ValidatePgDumpHeaderAsync(backupPath, cancellationToken);

            FileInfo backupFile = new(backupPath);
            if (!backupFile.Exists || backupFile.Length == 0)
            {
                throw new InvalidOperationException(
                    "PostgreSQL reported a successful backup, but the generated archive is empty.");
            }

            string downloadName =
                $"paladinhub-full-database-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.dump";

            logger.LogInformation(
                "Full PostgreSQL backup created successfully ({BackupSize} bytes).",
                backupFile.Length);

            return new DatabaseBackupArtifact(backupPath, downloadName);
        }
        catch
        {
            if (backupPath is not null)
            {
                TryDelete(backupPath);
            }

            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task RestoreBackupAsync(
        Stream archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);

        await operationLock.WaitAsync(cancellationToken);
        string? uploadedPath = null;
        string? sqlPath = null;
        string? resetPath = null;

        try
        {
            uploadedPath = CreateTemporaryPath("dump");

            await using (FileStream destination = new(
                uploadedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await archive.CopyToAsync(
                    destination,
                    CopyBufferSize,
                    cancellationToken);
            }

            FileInfo uploadedFile = new(uploadedPath);
            if (uploadedFile.Length == 0)
            {
                throw new InvalidDataException("The uploaded backup archive is empty.");
            }

            await ValidatePgDumpHeaderAsync(uploadedPath, cancellationToken);

            try
            {
                await RunPostgresToolAsync(
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

            // Materialize and decompress the complete archive before touching the database.
            sqlPath = CreateTemporaryPath("sql");
            resetPath = CreateTemporaryPath("sql");
            await RunPostgresToolAsync("pg_restore",
                ["--clean", "--if-exists", "--no-owner", "--no-privileges", "--file", sqlPath, uploadedPath],
                "read the complete database archive", cancellationToken);
            await File.WriteAllTextAsync(resetPath, ResetDatabaseSql, cancellationToken);
            NpgsqlConnection.ClearAllPools();
            try
            {
                // Both files execute in ONE transaction. Remove objects created after the
                // snapshot as well; pg_restore --clean alone leaves those objects behind.
                await RunPostgresToolAsync("psql",
                    ["--no-psqlrc", "--no-password", "--single-transaction",
                     "--set=ON_ERROR_STOP=on", "--file", resetPath, "--file", sqlPath],
                    "restore the database backup", cancellationToken);
            }
            finally
            {
                NpgsqlConnection.ClearAllPools();
            }

            logger.LogWarning(
                "Full PostgreSQL database restore completed successfully from an uploaded archive ({BackupSize} bytes).",
                uploadedFile.Length);
        }
        finally
        {
            if (uploadedPath is not null)
            {
                TryDelete(uploadedPath);
            }

            if (sqlPath is not null) TryDelete(sqlPath);
            if (resetPath is not null) TryDelete(resetPath);
            operationLock.Release();
        }
    }

    // Keep PostgreSQL system schemas and its built-in PL/pgSQL language. All application
    // schemas, extensions and large objects are rebuilt from the unfiltered full dump.
    private const string ResetDatabaseSql = """
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
        -- pg_dump can omit the default public schema, assuming initdb created it.
        CREATE SCHEMA public;
        GRANT USAGE ON SCHEMA public TO PUBLIC;
        """;

    private async Task RunPostgresToolAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        ApplyPostgresEnvironment(startInfo);

        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Unable to start {executable} while trying to {operation}.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL utility '{executable}' is not installed or is not available in PATH.",
                ex);
        }

        Task standardOutputTask = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        await standardOutputTask;
        string standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new PostgresToolException(
                executable,
                operation,
                process.ExitCode,
                standardError);
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            logger.LogDebug(
                "{PostgresTool} completed while trying to {Operation}: {ToolOutput}",
                executable,
                operation,
                standardError.Trim());
        }

        // pg_restore --list writes its table of contents to stdout. Reading it above is
        // intentional so the process cannot block on a full output pipe.

    }

    private void ApplyPostgresEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["PGCONNECT_TIMEOUT"] = "30";
        startInfo.Environment["PGHOST"] = connection.Host;
        startInfo.Environment["PGPORT"] = connection.Port.ToString();
        startInfo.Environment["PGDATABASE"] = connection.Database;
        startInfo.Environment["PGUSER"] = connection.Username;

        if (!string.IsNullOrEmpty(connection.Password))
        {
            startInfo.Environment["PGPASSWORD"] = connection.Password;
        }

        string sslMode = connection.SslMode.ToString();
        startInfo.Environment["PGSSLMODE"] = sslMode switch
        {
            "VerifyCA" => "verify-ca",
            "VerifyFull" => "verify-full",
            _ => sslMode.ToLowerInvariant()
        };
    }

    private static async Task ValidatePgDumpHeaderAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            PgDumpMagic.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] header = new byte[PgDumpMagic.Length];
        int totalRead = 0;

        while (totalRead < header.Length)
        {
            int read = await stream.ReadAsync(
                header.AsMemory(totalRead, header.Length - totalRead),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead != PgDumpMagic.Length || !header.SequenceEqual(PgDumpMagic))
        {
            throw new InvalidDataException(
                "The file is not a PostgreSQL custom-format backup archive.");
        }
    }

    private static string CreateTemporaryPath(string extension)
    {
        string fileName = $"paladinhub-db-{Guid.NewGuid():N}.{extension}";
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Temporary-file cleanup must never hide the original backup/restore result.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cancellation cleanup only.
        }
    }

    private sealed class PostgresToolException(
        string executable,
        string operation,
        int exitCode,
        string standardError)
        : InvalidOperationException(
            $"{executable} failed to {operation} with exit code {exitCode}: " +
            (string.IsNullOrWhiteSpace(standardError)
                ? "No error details were returned by PostgreSQL."
                : standardError.Trim()))
    {
    }
}

