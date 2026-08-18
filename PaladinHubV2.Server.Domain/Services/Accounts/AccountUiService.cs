using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountUiService : IAccountUiService
	{
		private readonly UserManager<User> _userManager;
		private readonly IHttpContextAccessor _http;
		private readonly IWalletService _wallet;
		private readonly IWebHostEnvironment _environment;

		public AccountUiService(
			UserManager<User> userManager,
			IHttpContextAccessor http,
			IWalletService wallet,
			IWebHostEnvironment environment)
		{
			_userManager = userManager;
			_http = http;
			_wallet = wallet;
			_environment = environment;
		}

		public async Task<User?> GetMe(ClaimsPrincipal principal)
		{
			if (principal == null)
			{
				return null;
			}

			return await _userManager.GetUserAsync(principal);
		}

		public string? GetUserId(ClaimsPrincipal principal) =>
			principal?.FindFirstValue(ClaimTypes.NameIdentifier);

		public (int score, string[] tips) ComputeSecurityScore(User me)
		{
			int score = 0;
			var tips = new List<string>();

			if (!string.IsNullOrWhiteSpace(me.Email))
			{
				score += me.EmailConfirmed ? 30 : 10;
				if (!me.EmailConfirmed)
				{
					tips.Add("Verify your email.");
				}
			}

			if (!string.IsNullOrWhiteSpace(me.PhoneNumber))
			{
				score += 15;
			}
			else
			{
				tips.Add("Add a phone number as a recovery factor.");
			}

			if (me.TwoFactorEnabled)
			{
				score += 40;
			}
			else
			{
				tips.Add("Enable two-factor authentication.");
			}

			if (!string.IsNullOrWhiteSpace(me.PasswordHash))
			{
				score += 15;
			}
			else
			{
				tips.Add("Set a strong account password.");
			}

			return (Math.Clamp(score, 0, 100), tips.ToArray());
		}

		public Task<decimal> GetBalance(string userId) =>
			_wallet.GetBalanceAsync(userId);

		public string? ReadRegionCookie()
		{
			HttpContext? context = _http.HttpContext;
			if (context?.Request?.Cookies == null)
			{
				return "US";
			}

			return context.Request.Cookies.TryGetValue(
					"region",
					out string? value) &&
				!string.IsNullOrWhiteSpace(value)
					? value
					: "US";
		}

		public string GetCurrencyForRegion(string region) => "USD";

		public string RegionDisplay(string region) => "United States";

		public IEnumerable<string> GetUserUploadedAvatars(string userId)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Enumerable.Empty<string>();
			}

			string webRoot = string.IsNullOrWhiteSpace(
					_environment.WebRootPath)
				? Path.Combine(
					_environment.ContentRootPath,
					"wwwroot")
				: _environment.WebRootPath;

			string root = Path.Combine(
				webRoot,
				"uploads",
				"avatars",
				userId);

			if (!Directory.Exists(root))
			{
				return Enumerable.Empty<string>();
			}

			return Directory
				.EnumerateFiles(root)
				.OrderByDescending(File.GetCreationTimeUtc)
				.Select(path =>
					$"/uploads/avatars/{userId}/" +
					Path.GetFileName(path));
		}

		public void RegisterUserUploadedAvatar(
			string userId,
			string webPath)
		{
			// Uploads are discovered directly from the filesystem.
		}

		public void UnregisterUserUploadedAvatar(
			string userId,
			string webPath)
		{
			// Uploads are discovered directly from the filesystem.
		}
	}
}
