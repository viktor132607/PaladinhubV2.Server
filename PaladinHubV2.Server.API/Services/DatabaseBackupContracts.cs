using System.Diagnostics;

namespace PaladinHubV2.Server.API.Services;

public sealed record DatabaseBackupArtifact(
    string FilePath,
    string FileName);

public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardError);

public interface IDatabaseBackupFileStore
{
    string CreateTemporaryPath(string extension);

    Task CopyToNewFileAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken);

    Task WriteAllTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken);

    long GetLength(string path);

    void TryDelete(string path);
}

public interface IPgDumpArchiveValidator
{
    Task ValidateAsync(
        string filePath,
        CancellationToken cancellationToken);
}

public interface IPostgresToolRunner
{
    Task RunAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        string operation,
        CancellationToken cancellationToken);
}

public interface IExternalProcessExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken);
}

public interface IDatabasePoolManager
{
    void Clear();
}

public sealed class PostgresToolException(
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
