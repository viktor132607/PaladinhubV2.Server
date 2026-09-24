using Microsoft.AspNetCore.Http;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountAvatarService : IAccountAvatarService
{
    private const long MaximumAvatarBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private readonly AppDbContext _db;
    private readonly IAccountAvatarStore _store;

    public AccountAvatarService(
        AppDbContext db,
        IAccountAvatarStore store)
    {
        _db = db;
        _store = store;
    }

    public async Task<AccountAvatarResult> UploadAvatarAsync(
        User user,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null ||
            file.Length == 0 ||
            file.Length > MaximumAvatarBytes)
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.NoFile,
                "No file was provided.");
        }

        string extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.UnsupportedFormat,
                "Unsupported image format.");
        }

        string path = await _store.SaveAsync(
            user.Id,
            extension,
            file,
            cancellationToken);

        RegisterUserUploadedAvatar(user.Id, path);
        return AccountAvatarResult.Success(path);
    }

    public async Task<AccountAvatarResult> SetUploadedAvatarAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryResolveOwnedUpload(
                user.Id,
                path,
                out string fullPath))
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.InvalidPath,
                "Invalid avatar path.");
        }

        if (!_store.Exists(fullPath))
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.NotFound,
                "Avatar file was not found.");
        }

        user.AvatarPath = path;
        _db.Update(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AccountAvatarResult.Success(path);
    }

    public async Task<AccountAvatarResult> DeleteUploadAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryResolveOwnedUpload(
                user.Id,
                path,
                out string fullPath))
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.InvalidPath,
                "Invalid avatar path.");
        }

        if (!_store.Exists(fullPath))
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.NotFound,
                "Avatar file was not found.");
        }

        _store.Delete(fullPath);
        UnregisterUserUploadedAvatar(user.Id, path!);

        if (string.Equals(
                user.AvatarPath,
                path,
                StringComparison.OrdinalIgnoreCase))
        {
            user.AvatarPath = null;
            _db.Update(user);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return AccountAvatarResult.Success();
    }

    public async Task<AccountAvatarResult> SetDefaultAvatarAsync(
        User user,
        string? file,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(file) ||
            Path.GetFileName(file) != file)
        {
            return AccountAvatarResult.Fail(
                AccountAvatarFailure.InvalidDefaultAvatar,
                "Invalid avatar file.");
        }

        user.AvatarPath = $"/images/avatars/{file}";
        _db.Update(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AccountAvatarResult.Success(user.AvatarPath);
    }

    public IEnumerable<string> GetUserUploadedAvatars(
        string userId) =>
        _store.List(userId);

    public void RegisterUserUploadedAvatar(
        string userId,
        string webPath)
    {
    }

    public void UnregisterUserUploadedAvatar(
        string userId,
        string webPath)
    {
    }
}
