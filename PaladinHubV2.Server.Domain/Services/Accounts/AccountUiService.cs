using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountUiService : IAccountUiService
{
    private readonly IAccountIdentityService _identity;
    private readonly IAccountOverviewService _overview;
    private readonly IAccountAvatarService _avatars;
    private readonly IAccountProfileService _profile;
    private readonly IAccountSecurityScorer _security;
    private readonly IAccountRegionService _region;

    public AccountUiService(
        IAccountIdentityService identity,
        IAccountOverviewService overview,
        IAccountAvatarService avatars,
        IAccountProfileService profile,
        IAccountSecurityScorer security,
        IAccountRegionService region)
    {
        _identity = identity;
        _overview = overview;
        _avatars = avatars;
        _profile = profile;
        _security = security;
        _region = region;
    }

    public Task<User?> GetMe(ClaimsPrincipal principal) =>
        _identity.GetMeAsync(principal);

    public string? GetUserId(ClaimsPrincipal principal) =>
        _identity.GetUserId(principal);

    public Task<MyAccountViewModel> BuildMyAccountAsync(
        User user,
        CancellationToken cancellationToken = default) =>
        _overview.BuildMyAccountAsync(user, cancellationToken);

    public Task<MyAccountViewModel> BuildOverviewAsync(
        User user,
        int page,
        CancellationToken cancellationToken = default) =>
        _overview.BuildOverviewAsync(user, page, cancellationToken);

    public Task MarkPhoneVerifiedAsync(
        User user,
        CancellationToken cancellationToken = default) =>
        _profile.MarkPhoneVerifiedAsync(user, cancellationToken);

    public Task<AccountAvatarResult> UploadAvatarAsync(
        User user,
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        _avatars.UploadAvatarAsync(user, file, cancellationToken);

    public Task<AccountAvatarResult> SetUploadedAvatarAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default) =>
        _avatars.SetUploadedAvatarAsync(user, path, cancellationToken);

    public Task<AccountAvatarResult> DeleteUploadAsync(
        User user,
        string? path,
        CancellationToken cancellationToken = default) =>
        _avatars.DeleteUploadAsync(user, path, cancellationToken);

    public Task<AccountAvatarResult> SetDefaultAvatarAsync(
        User user,
        string? file,
        CancellationToken cancellationToken = default) =>
        _avatars.SetDefaultAvatarAsync(user, file, cancellationToken);

    public (int score, string[] tips) ComputeSecurityScore(User me) =>
        _security.Compute(me);

    public Task<decimal> GetBalance(string userId) =>
        _overview.GetBalanceAsync(userId);

    public string? ReadRegionCookie() =>
        _region.ReadRegionCookie();

    public string GetCurrencyForRegion(string region) =>
        _region.GetCurrencyForRegion(region);

    public string RegionDisplay(string region) =>
        _region.RegionDisplay(region);

    public IEnumerable<string> GetUserUploadedAvatars(string userId) =>
        _avatars.GetUserUploadedAvatars(userId);

    public void RegisterUserUploadedAvatar(
        string userId,
        string webPath) =>
        _avatars.RegisterUserUploadedAvatar(userId, webPath);

    public void UnregisterUserUploadedAvatar(
        string userId,
        string webPath) =>
        _avatars.UnregisterUserUploadedAvatar(userId, webPath);
}
