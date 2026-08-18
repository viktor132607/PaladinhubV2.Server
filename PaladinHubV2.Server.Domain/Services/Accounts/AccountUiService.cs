using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public class AccountUiService : IAccountUiService
	{
		private readonly UserManager<User> _userManager;
		private readonly IHttpContextAccessor _http;
		private readonly IWalletService _wallet;

		public AccountUiService(UserManager<User> userManager, IHttpContextAccessor http, IWalletService wallet)
		{
			_userManager = userManager;
			_http = http;
			_wallet = wallet;
		}

		public async Task<User?> GetMe(ClaimsPrincipal principal)
		{
			if (principal == null) return null;
			return await _userManager.GetUserAsync(principal);
		}

		public string? GetUserId(ClaimsPrincipal principal)
			=> principal?.FindFirstValue(ClaimTypes.NameIdentifier);

		public (int score, string[] tips) ComputeSecurityScore(User me)
		{
			var score = 0;
			var tips = new List<string>();

			if (!string.IsNullOrWhiteSpace(me.Email))
			{
				score += me.EmailConfirmed ? 30 : 10;
				if (!me.EmailConfirmed) tips.Add("Verify your email.");
			}

			if (!string.IsNullOrWhiteSpace(me.PhoneNumber))
				score += 15;
			else
				tips.Add("Add a phone number as a recovery factor.");

			if (me.TwoFactorEnabled) score += 40;
			else tips.Add("Enable two-factor authentication.");

			if (!string.IsNullOrWhiteSpace(me.PasswordHash)) score += 15;
			else tips.Add("Set a strong account password.");

			score = Math.Clamp(score, 0, 100);
			return (score, tips.ToArray());
		}

		// Wallet
		public Task<decimal> GetBalance(string userId) => _wallet.GetBalanceAsync(userId);

		// Region/Currency compatibility (fixed to USD/US)
		public string? ReadRegionCookie()
		{
			// държим съвместимост – ако има cookie, връщаме него; иначе US
			var ctx = _http.HttpContext;
			if (ctx?.Request?.Cookies == null) return "US";
			return ctx.Request.Cookies.TryGetValue("region", out var v) && !string.IsNullOrWhiteSpace(v) ? v : "US";
		}
		public string GetCurrencyForRegion(string region) => "USD";
		public string RegionDisplay(string region) => "United States";

		// Avatars
		public IEnumerable<string> GetUserUploadedAvatars(string userId)
		{
			if (string.IsNullOrWhiteSpace(userId)) return Enumerable.Empty<string>();
			var root = Path.Combine("wwwroot", "uploads", "avatars", userId);
			if (!Directory.Exists(root)) return Enumerable.Empty<string>();
			return Directory.EnumerateFiles(root)
				.OrderByDescending(File.GetCreationTimeUtc)
				.Select(p => "/uploads/avatars/" + userId + "/" + Path.GetFileName(p));
		}

		public void RegisterUserUploadedAvatar(string userId, string webPath)
		{
			// Нямаме отделна таблица – UI-то чете директно от файловата система.
			// Методът остава no-op за съвместимост.
		}

		public void UnregisterUserUploadedAvatar(string userId, string webPath)
		{
			// No-op – файловете се трият от контролера; списъкът се чете от диска.
		}
	}
}
