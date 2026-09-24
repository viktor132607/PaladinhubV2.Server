using System.ComponentModel;
using System.Diagnostics;

namespace PaladinHubV2.Server.API.Services;

public sealed class SystemExternalProcessExecutor :
    IExternalProcessExecutor
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Unable to start {startInfo.FileName}.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL utility '{startInfo.FileName}' is not installed or is not available in PATH.",
                ex);
        }

        Task standardOutputTask =
            process.StandardOutput.BaseStream
                .CopyToAsync(Stream.Null);

        Task<string> standardErrorTask =
            process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            await process.WaitForExitAsync(
                CancellationToken.None);

            await Task.WhenAll(
                standardOutputTask,
                standardErrorTask);

            throw;
        }

        await standardOutputTask;

        string standardError =
            await standardErrorTask;

        return new ProcessExecutionResult(
            process.ExitCode,
            standardError);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cancellation cleanup only.
        }
    }
}
