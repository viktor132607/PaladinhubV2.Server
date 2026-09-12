using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountUiService
	{
		Task<User?> GetMe(ClaimsPrincipal principal);
		string? GetUserId(ClaimsPrincipal principal);

		Task<MyAccountViewModel> BuildMyAccountAsync(
			User user,
			CancellationToken cancellationToken = default);

		Task<MyAccountViewModel> BuildOverviewAsync(
			User user,
			int page,
			CancellationToken cancellationToken = default);

		Task MarkPhoneVerifiedAsync(
			User user,
			CancellationToken cancellationToken = default);

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

		(int score, string[] tips) ComputeSecurityScore(User me);
		Task<decimal> GetBalance(string userId);

		string? ReadRegionCookie();
		string GetCurrencyForRegion(string region);
		string RegionDisplay(string region);

		IEnumerable<string> GetUserUploadedAvatars(string userId);
		void RegisterUserUploadedAvatar(string userId, string webPath);
		void UnregisterUserUploadedAvatar(string userId, string webPath);
	}
}
