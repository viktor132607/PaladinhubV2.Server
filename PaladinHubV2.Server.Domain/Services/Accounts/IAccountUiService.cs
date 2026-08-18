using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountUiService
	{
		Task<User?> GetMe(ClaimsPrincipal principal);
		string? GetUserId(ClaimsPrincipal principal);

		(int score, string[] tips) ComputeSecurityScore(User me);

		// Wallet helpers used по контролерите
		Task<decimal> GetBalance(string userId);

		// Region/Currency – оставени за съвместимост; реално връщаме USD и US
		string? ReadRegionCookie();
		string GetCurrencyForRegion(string region);
		string RegionDisplay(string region);

		// Avatars (UI helpers)
		IEnumerable<string> GetUserUploadedAvatars(string userId);
		void RegisterUserUploadedAvatar(string userId, string webPath);
		void UnregisterUserUploadedAvatar(string userId, string webPath);
	}
}
