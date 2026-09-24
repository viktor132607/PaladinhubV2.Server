namespace PaladinHubV2.Server.API.Services;

public sealed class PhysicalDatabaseBackupFileStore :
    IDatabaseBackupFileStore
{
    private const int CopyBufferSize = 128 * 1024;

    public string CreateTemporaryPath(string extension)
    {
        string fileName =
            $"paladinhub-db-{Guid.NewGuid():N}.{extension}";

        return Path.Combine(
            Path.GetTempPath(),
            fileName);
    }

    public async Task CopyToNewFileAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        await source.CopyToAsync(
            destination,
            CopyBufferSize,
            cancellationToken);
    }

    public Task WriteAllTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(
            path,
            content,
            cancellationToken);

    public long GetLength(string path)
    {
        FileInfo file = new(path);
        return file.Exists ? file.Length : 0;
    }

    public void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary-file cleanup must never hide
            // the original backup/restore result.
        }
    }
}
