using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public interface IAccountIdentityService
{
    Task<User?> GetMeAsync(ClaimsPrincipal principal);
    string? GetUserId(ClaimsPrincipal principal);
}

public interface IAccountOverviewService
{
    Task<MyAccountViewModel> BuildMyAccountAsync(
        User user,
        CancellationToken cancellationToken = default);

    Task<MyAccountViewModel> BuildOverviewAsync(
        User user,
        int page,
        CancellationToken cancellationToken = default);

    Task<decimal> GetBalanceAsync(string userId);
}

public interface IAccountAvatarService
{
    Task<AccountAvatarResult> UploadAvatarAsync(
        User user,
        IFormFile? file,
        CancellationToken cancellationToken = default);

    Task<AccountAvatarResult> SetUploadedAvatarAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default);

    Task<AccountAvatarResult> DeleteUploadAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default);

    Task<AccountAvatarResult> SetDefaultAvatarAsync(
        User user,
        string? file,
        CancellationToken cancellationToken = default);

    IEnumerable<string> GetUserUploadedAvatars(string userId);
    void RegisterUserUploadedAvatar(string userId, string webPath);
    void UnregisterUserUploadedAvatar(string userId, string webPath);
}

public interface IAccountAvatarStore
{
    Task<string> SaveAsync(
        string userId,
        string extension,
        IFormFile file,
        CancellationToken cancellationToken);

    bool TryResolveOwnedUpload(
        string userId,
        string? webPath,
        out string fullPath);

    bool Exists(string fullPath);
    void Delete(string fullPath);
    IEnumerable<string> List(string userId);
}

public interface IAccountProfileService
{
    Task MarkPhoneVerifiedAsync(
        User user,
        CancellationToken cancellationToken = default);
}

public interface IAccountSecurityScorer
{
    (int score, string[] tips) Compute(User user);
}

public interface IAccountRegionService
{
    string? ReadRegionCookie();
    string GetCurrencyForRegion(string region);
    string RegionDisplay(string region);
}
