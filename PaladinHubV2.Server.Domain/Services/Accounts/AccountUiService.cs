using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public class AccountUiService : IAccountUiService
	{
		private const string Currency = "USD";
		private const int OverviewPageSize = 5;

		private readonly UserManager<User> _userManager;
		private readonly IHttpContextAccessor _http;
		private readonly IWalletService _wallet;
		private readonly AppDbContext _db;

		public AccountUiService(
			UserManager<User> userManager,
			IHttpContextAccessor http,
			IWalletService wallet,
			AppDbContext db)
		{
			_userManager = userManager;
			_http = http;
			_wallet = wallet;
			_db = db;
		}

		public async Task<User?> GetMe(ClaimsPrincipal principal)
		{
			if (principal == null)
			{
				return null;
			}

			return await _userManager.GetUserAsync(principal);
		}

		public string? GetUserId(ClaimsPrincipal principal)
		{
			return principal?.FindFirstValue(
				ClaimTypes.NameIdentifier);
		}

		public async Task<MyAccountViewModel> BuildMyAccountAsync(
			User user,
			CancellationToken cancellationToken = default)
		{
			decimal balance =
				await _wallet.GetBalanceAsync(user.Id);

			List<Transaction> recent =
				await _db.Transactions
					.AsNoTracking()
					.Where(transaction =>
						transaction.UserId == user.Id)
					.OrderByDescending(transaction =>
						transaction.CreatedAtUtc)
					.Take(OverviewPageSize)
					.ToListAsync(cancellationToken);

			var (score, tips) =
				ComputeSecurityScore(user);

			return new MyAccountViewModel
			{
				Currency = Currency,
				Balance = balance,
				RecentPurchases = recent,
				Page = 1,
				TotalPages = 1,
				SecurityScore = score,
				SecurityTips = tips,
				Uploads =
					GetUserUploadedAvatars(user.Id).ToList()
			};
		}

		public async Task<MyAccountViewModel> BuildOverviewAsync(
			User user,
			int page,
			CancellationToken cancellationToken = default)
		{
			page = Math.Max(page, 1);

			var query = _db.Transactions
				.AsNoTracking()
				.Where(transaction =>
					transaction.UserId == user.Id)
				.OrderByDescending(transaction =>
					transaction.CreatedAtUtc);

			int total =
				await query.CountAsync(cancellationToken);

			int totalPages = Math.Max(
				1,
				(int)Math.Ceiling(
					total / (double)OverviewPageSize));

			page = Math.Clamp(page, 1, totalPages);

			List<Transaction> recent =
				await query
					.Skip((page - 1) * OverviewPageSize)
					.Take(OverviewPageSize)
					.ToListAsync(cancellationToken);

			decimal balance =
				await _wallet.GetBalanceAsync(user.Id);

			var (score, tips) =
				ComputeSecurityScore(user);

			return new MyAccountViewModel
			{
				Currency = Currency,
				Balance = balance,
				RecentPurchases = recent,
				Page = page,
				TotalPages = totalPages,
				SecurityScore = score,
				SecurityTips = tips,
				Uploads =
					GetUserUploadedAvatars(user.Id).ToList()
			};
		}

		public async Task MarkPhoneVerifiedAsync(
			User user,
			CancellationToken cancellationToken = default)
		{
			user.PhoneNumberConfirmed = true;
			_db.Update(user);
			await _db.SaveChangesAsync(cancellationToken);
		}

		public async Task<AccountAvatarResult> UploadAvatarAsync(
			User user,
			IFormFile? file,
			CancellationToken cancellationToken = default)
		{
			if (file == null || file.Length == 0)
			{
				return AccountAvatarResult.Fail(
					AccountAvatarFailure.NoFile,
					"No file was provided.");
			}

			string extension = Path
				.GetExtension(file.FileName)
				.ToLowerInvariant();

			if (extension is not ".jpg"
				and not ".jpeg"
				and not ".png"
				and not ".webp")
			{
				return AccountAvatarResult.Fail(
					AccountAvatarFailure.UnsupportedFormat,
					"Unsupported image format.");
			}

			string uploadsRoot =
				GetAvatarDirectory(user.Id);

			Directory.CreateDirectory(uploadsRoot);

			string fileName =
				$"{Guid.NewGuid():N}{extension}";

			string fullPath =
				Path.Combine(uploadsRoot, fileName);

			await using (FileStream stream =
				System.IO.File.Create(fullPath))
			{
				await file.CopyToAsync(
					stream,
					cancellationToken);
			}

			string webPath =
				$"/uploads/avatars/{user.Id}/{fileName}";

			RegisterUserUploadedAvatar(user.Id, webPath);

			return AccountAvatarResult.Success(webPath);
		}

		public async Task<AccountAvatarResult>
			SetUploadedAvatarAsync(
				User user,
				string? path,
				CancellationToken cancellationToken = default)
		{
			if (!TryResolveOwnedUpload(
					user.Id,
					path,
					out string fullPath))
			{
				return AccountAvatarResult.Fail(
					AccountAvatarFailure.InvalidPath,
					"Invalid avatar path.");
			}

			if (!System.IO.File.Exists(fullPath))
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
			if (!TryResolveOwnedUpload(
					user.Id,
					path,
					out string fullPath))
			{
				return AccountAvatarResult.Fail(
					AccountAvatarFailure.InvalidPath,
					"Invalid avatar path.");
			}

			if (!System.IO.File.Exists(fullPath))
			{
				return AccountAvatarResult.Fail(
					AccountAvatarFailure.NotFound,
					"Avatar file was not found.");
			}

			System.IO.File.Delete(fullPath);
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

		public (int score, string[] tips)
			ComputeSecurityScore(User me)
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
				tips.Add(
					"Add a phone number as a recovery factor.");
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

			score = Math.Clamp(score, 0, 100);
			return (score, tips.ToArray());
		}

		public Task<decimal> GetBalance(string userId)
		{
			return _wallet.GetBalanceAsync(userId);
		}

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

		public string GetCurrencyForRegion(string region)
		{
			return Currency;
		}

		public string RegionDisplay(string region)
		{
			return "United States";
		}

		public IEnumerable<string> GetUserUploadedAvatars(
			string userId)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Enumerable.Empty<string>();
			}

			string root = Path.Combine(
				"wwwroot",
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
					"/uploads/avatars/" +
					userId +
					"/" +
					Path.GetFileName(path));
		}

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

		private static string GetAvatarDirectory(string userId)
		{
			return Path.Combine(
				Directory.GetCurrentDirectory(),
				"wwwroot",
				"uploads",
				"avatars",
				userId);
		}

		private static bool TryResolveOwnedUpload(
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

			try
			{
				string fileName = Path.GetFileName(webPath);

				if (string.IsNullOrWhiteSpace(fileName))
				{
					return false;
				}

				string avatarDirectory = Path.GetFullPath(
					GetAvatarDirectory(userId));

				string candidate = Path.GetFullPath(
					Path.Combine(avatarDirectory, fileName));

				if (!candidate.StartsWith(
						avatarDirectory +
						Path.DirectorySeparatorChar,
						StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}

				fullPath = candidate;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
