using Microsoft.AspNetCore.Http;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class PhysicalAccountAvatarStore : IAccountAvatarStore
{
    private readonly string _contentRoot;

    public PhysicalAccountAvatarStore()
        : this(Directory.GetCurrentDirectory())
    {
    }

    internal PhysicalAccountAvatarStore(string contentRoot)
    {
        _contentRoot = contentRoot;
    }

    public async Task<string> SaveAsync(
        string userId,
        string extension,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        string directory = GetAvatarDirectory(userId);
        Directory.CreateDirectory(directory);

        string fileName = $"{Guid.NewGuid():N}{extension}";
        string fullPath = Path.Combine(directory, fileName);

        await using FileStream stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/avatars/{userId}/{fileName}";
    }

    public bool TryResolveOwnedUpload(
        string userId,
        string? webPath,
        out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(webPath))
        {
            return false;
        }

        string expectedPrefix =
            $"/uploads/avatars/{userId}/";

        if (!webPath.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (webPath.IndexOf('\0') >= 0)
        {
            return false;
        }

        string fileName = Path.GetFileName(webPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string directory =
            Path.GetFullPath(GetAvatarDirectory(userId));

        fullPath = Path.GetFullPath(
            Path.Combine(directory, fileName));

        return true;
    }

    public bool Exists(string fullPath) =>
        File.Exists(fullPath);

    public void Delete(string fullPath) =>
        File.Delete(fullPath);

    public IEnumerable<string> List(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Enumerable.Empty<string>();
        }

        string directory = GetAvatarDirectory(userId);

        if (!Directory.Exists(directory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(directory)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Select(path =>
                $"/uploads/avatars/{userId}/{Path.GetFileName(path)}")
            .ToArray();
    }

    private string GetAvatarDirectory(string userId) =>
        Path.Combine(
            _contentRoot,
            "wwwroot",
            "uploads",
            "avatars",
            userId);
}
