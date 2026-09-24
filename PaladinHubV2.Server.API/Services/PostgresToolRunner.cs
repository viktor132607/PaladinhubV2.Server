using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace PaladinHubV2.Server.API.Services;

public sealed class PostgresToolRunner :
    IPostgresToolRunner
{
    private readonly NpgsqlConnectionStringBuilder _connection;
    private readonly IExternalProcessExecutor _executor;
    private readonly ILogger _logger;

    public PostgresToolRunner(
        string connectionString,
        IExternalProcessExecutor executor,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(logger);

        _connection =
            new NpgsqlConnectionStringBuilder(
                connectionString);

        if (string.IsNullOrWhiteSpace(_connection.Host) ||
            string.IsNullOrWhiteSpace(_connection.Database) ||
            string.IsNullOrWhiteSpace(_connection.Username))
        {
            throw new InvalidOperationException(
                "Database backup requires PostgreSQL host, database, and username configuration.");
        }

        _executor = executor;
        _logger = logger;
    }

    public async Task RunAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo =
            BuildStartInfo(
                executable,
                arguments);

        ProcessExecutionResult result =
            await _executor.ExecuteAsync(
                startInfo,
                cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new PostgresToolException(
                executable,
                operation,
                result.ExitCode,
                result.StandardError);
        }

        if (!string.IsNullOrWhiteSpace(
                result.StandardError))
        {
            _logger.LogDebug(
                "{PostgresTool} completed while trying to {Operation}: {ToolOutput}",
                executable,
                operation,
                result.StandardError.Trim());
        }
    }

    private ProcessStartInfo BuildStartInfo(
        string executable,
        IReadOnlyCollection<string> arguments)
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

        ApplyEnvironment(startInfo);
        return startInfo;
    }

    private void ApplyEnvironment(
        ProcessStartInfo startInfo)
    {
        startInfo.Environment["PGCONNECT_TIMEOUT"] =
            "30";
        startInfo.Environment["PGHOST"] =
            _connection.Host;
        startInfo.Environment["PGPORT"] =
            _connection.Port.ToString();
        startInfo.Environment["PGDATABASE"] =
            _connection.Database;
        startInfo.Environment["PGUSER"] =
            _connection.Username;

        if (!string.IsNullOrEmpty(
                _connection.Password))
        {
            startInfo.Environment["PGPASSWORD"] =
                _connection.Password;
        }

        string sslMode =
            _connection.SslMode.ToString();

        startInfo.Environment["PGSSLMODE"] =
            sslMode switch
            {
                "VerifyCA" => "verify-ca",
                "VerifyFull" => "verify-full",
                _ => sslMode.ToLowerInvariant()
            };
    }
}
