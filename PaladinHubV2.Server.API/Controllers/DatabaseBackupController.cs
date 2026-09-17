using PaladinHubV2.Server.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(Roles = "Admin")]
[Route("api/admin/database-backup")]
public sealed class DatabaseBackupController(
    DatabaseBackupService databaseBackupService,
    ILogger<DatabaseBackupController> logger) : ControllerBase
{
    [HttpGet("export")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        DatabaseBackupArtifact backup =
            await databaseBackupService.CreateBackupAsync(cancellationToken);

        try
        {
            FileStream stream = new(
                backup.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan |
                FileOptions.DeleteOnClose);

            return File(
                stream,
                "application/octet-stream",
                backup.FileName,
                enableRangeProcessing: false);
        }
        catch
        {
            try
            {
                System.IO.File.Delete(backup.FilePath);
            }
            catch
            {
                // Best-effort cleanup only.
            }

            throw;
        }
    }

    [HttpPost("restore")]
    [Consumes("multipart/form-data")]
    [ValidateAntiForgeryToken]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Restore(
        [FromForm] IFormFile? archive,
        [FromForm] string? confirmation,
        CancellationToken cancellationToken)
    {
        if (confirmation != "RESTORE")
            return BadRequest(new { message = "Type RESTORE to confirm replacing the entire database." });

        if (archive is null || archive.Length == 0)
        {
            return BadRequest(new
            {
                message = "A non-empty PostgreSQL backup archive is required."
            });
        }

        try
        {
            await using Stream archiveStream = archive.OpenReadStream();
            await databaseBackupService.RestoreBackupAsync(
                archiveStream,
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        logger.LogWarning(
            "An administrator restored the full database from uploaded archive {ArchiveName} ({ArchiveSize} bytes).",
            Path.GetFileName(archive.FileName),
            archive.Length);

        return Ok(new
        {
            message = "Database restored successfully.",
            restoredAtUtc = DateTime.UtcNow
        });
    }
}

