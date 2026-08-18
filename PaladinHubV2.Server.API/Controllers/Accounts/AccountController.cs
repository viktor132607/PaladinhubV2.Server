using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Wallet;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountController : ControllerBase
	{
		private const string Currency = "USD";

		private readonly AppDbContext _db;
		private readonly IAccountUiService _ui;
		private readonly IPromoCodeService _promo;
		private readonly IWalletService _wallet;
		private readonly SignInManager<User> _signInManager;

		public AccountController(
			AppDbContext db,
			IAccountUiService ui,
			IPromoCodeService promo,
			IWalletService wallet,
			SignInManager<User> signInManager)
		{
			_db = db;
			_ui = ui;
			_promo = promo;
			_wallet = wallet;
			_signInManager = signInManager;
		}

		private Task<User?> Me() => _ui.GetMe(User);

		[HttpGet("MyAccount")]
		public async Task<IActionResult> MyAccount()
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			var balance = await _wallet.GetBalanceAsync(me.Id);

			var recent = await _db.Transactions
				.AsNoTracking()
				.Where(t => t.UserId == me.Id)
				.OrderByDescending(t => t.CreatedAtUtc)
				.Take(5)
				.ToListAsync();

			var (score, tips) = _ui.ComputeSecurityScore(me);

			var model = new MyAccountViewModel
			{
				Currency = Currency,
				Balance = balance,
				RecentPurchases = recent,
				Page = 1,
				TotalPages = 1,
				SecurityScore = score,
				SecurityTips = tips,
				Uploads = _ui.GetUserUploadedAvatars(me.Id).ToList()
			};

			return Ok(model);
		}

		[HttpGet("Overview")]
		public async Task<IActionResult> Overview([FromQuery] int page = 1)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			const int pageSize = 5;

			page = Math.Max(page, 1);

			var query = _db.Transactions
				.AsNoTracking()
				.Where(t => t.UserId == me.Id)
				.OrderByDescending(t => t.CreatedAtUtc);

			var total = await query.CountAsync();

			var totalPages = Math.Max(
				1,
				(int)Math.Ceiling(total / (double)pageSize));

			page = Math.Clamp(page, 1, totalPages);

			var recent = await query
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var balance = await _wallet.GetBalanceAsync(me.Id);
			var (score, tips) = _ui.ComputeSecurityScore(me);

			var model = new MyAccountViewModel
			{
				Currency = Currency,
				Balance = balance,
				RecentPurchases = recent,
				Page = page,
				TotalPages = totalPages,
				SecurityScore = score,
				SecurityTips = tips,
				Uploads = _ui.GetUserUploadedAvatars(me.Id).ToList()
			};

			return Ok(model);
		}

		[HttpGet("Settings")]
		public IActionResult Settings() => NoContent();

		[HttpGet("AccountDetails")]
		public IActionResult AccountDetails() => NoContent();

		[HttpGet("Privacy")]
		public IActionResult Privacy() => NoContent();

		[HttpGet("Connections")]
		public IActionResult Connections() => NoContent();

		[HttpPost("RedeemCode")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RedeemCode([FromForm] string code)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			if (string.IsNullOrWhiteSpace(code))
			{
				return BadRequest(new
				{
					ok = false,
					reason = "empty",
					message = "Code is required."
				});
			}

			var result = await _promo.RedeemAsync(
				me,
				code,
				Currency);

			var reason = result.ok
				? "success"
				: result.msg.Contains(
					"already",
					StringComparison.OrdinalIgnoreCase)
					? "already-used"
					: "invalid";

			if (!result.ok)
			{
				var error = new
				{
					ok = false,
					reason,
					message = result.msg,
					amount = result.amount,
					currency = result.currency,
					percent = result.percent
				};

				return reason == "already-used"
					? Conflict(error)
					: BadRequest(error);
			}

			if (result.percent.HasValue)
			{
				HttpContext.Session.SetInt32(
					"cart_discount_percent",
					result.percent.Value);
			}

			return Ok(new
			{
				ok = true,
				reason,
				message = result.msg,
				amount = result.amount,
				currency = result.currency,
				percent = result.percent
			});
		}

		[HttpPost("DevTopUp")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DevTopUp(
			[FromForm] decimal amount)
		{
			if (amount <= 0m)
			{
				return BadRequest(new
				{
					message = "Amount must be greater than zero."
				});
			}

			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			var transactionId = await _wallet.TopUpAsync(
				me.Id,
				amount,
				"Balance Top-up");

			var balance = await _wallet.GetBalanceAsync(me.Id);

			return Ok(new
			{
				ok = true,
				transactionId,
				amount,
				balance,
				currency = Currency
			});
		}

		[HttpPost("UploadAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UploadAvatar(
			[FromForm] IFormFile file)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			if (file == null || file.Length == 0)
			{
				return BadRequest(new
				{
					ok = false,
					message = "No file was provided."
				});
			}

			var extension = Path
				.GetExtension(file.FileName)
				.ToLowerInvariant();

			if (extension is not ".jpg"
				and not ".jpeg"
				and not ".png"
				and not ".webp")
			{
				return StatusCode(
					StatusCodes.Status415UnsupportedMediaType,
					new
					{
						ok = false,
						message = "Unsupported image format."
					});
			}

			var uploadsRoot = GetAvatarDirectory(me.Id);

			Directory.CreateDirectory(uploadsRoot);

			var fileName = $"{Guid.NewGuid():N}{extension}";
			var fullPath = Path.Combine(uploadsRoot, fileName);

			await using (var stream = System.IO.File.Create(fullPath))
			{
				await file.CopyToAsync(stream);
			}

			var webPath = $"/uploads/avatars/{me.Id}/{fileName}";

			_ui.RegisterUserUploadedAvatar(me.Id, webPath);

			return Ok(new
			{
				ok = true,
				path = webPath
			});
		}

		[HttpPost("SetUploadedAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetUploadedAvatar(
			[FromForm] string path)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			if (!TryResolveOwnedUpload(
					me.Id,
					path,
					out var fullPath))
			{
				return BadRequest(new
				{
					ok = false,
					message = "Invalid avatar path."
				});
			}

			if (!System.IO.File.Exists(fullPath))
			{
				return NotFound(new
				{
					ok = false,
					message = "Avatar file was not found."
				});
			}

			me.AvatarPath = path;

			_db.Update(me);
			await _db.SaveChangesAsync();

			return Ok(new
			{
				ok = true,
				path
			});
		}

		[HttpPost("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteUpload(
			[FromForm] string path)
		{
			return DeleteUploadCore(path);
		}

		[HttpDelete("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteUploadByQuery(
			[FromQuery] string path)
		{
			return DeleteUploadCore(path);
		}

		[HttpPost("SetDefaultAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetDefaultAvatar(
			[FromForm] string file)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			if (string.IsNullOrWhiteSpace(file)
				|| Path.GetFileName(file) != file)
			{
				return BadRequest(new
				{
					ok = false,
					message = "Invalid avatar file."
				});
			}

			me.AvatarPath = $"/images/avatars/{file}";

			_db.Update(me);
			await _db.SaveChangesAsync();

			return Ok(new
			{
				ok = true,
				path = me.AvatarPath
			});
		}

		[HttpPost("Logout")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();

			return Ok(new { ok = true });
		}

		[HttpPost("MarkPhoneVerified")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkPhoneVerified()
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			me.PhoneNumberConfirmed = true;

			_db.Update(me);
			await _db.SaveChangesAsync();

			return Ok(new
			{
				ok = true,
				phoneNumberConfirmed = true
			});
		}

		[HttpGet("EditProfile")]
		public IActionResult EditProfile()
		{
			return NotImplemented(
				"Profile editing is not implemented yet.");
		}

		[HttpGet("EditEmail")]
		public IActionResult EditEmail()
		{
			return NotImplemented(
				"Email change is not implemented yet.");
		}

		[HttpGet("EditPhone")]
		public IActionResult EditPhone()
		{
			return NotImplemented(
				"Phone update is not implemented yet.");
		}

		[HttpGet("RemovePhone")]
		public IActionResult RemovePhone()
		{
			return NotImplemented(
				"Phone removal is not implemented yet.");
		}

		[HttpGet("EditBattleTag")]
		public IActionResult EditBattleTag()
		{
			return NotImplemented(
				"BattleTag change is not supported.");
		}

		[HttpGet("AddAddress")]
		public IActionResult AddAddress()
		{
			return NotImplemented(
				"Address creation is not implemented yet.");
		}

		[HttpGet("EditAddress")]
		public IActionResult EditAddress()
		{
			return NotImplemented(
				"Address editing is not implemented yet.");
		}

		[HttpGet("ConnectProvider")]
		public IActionResult ConnectProvider(
			[FromQuery] string provider)
		{
			return NotImplemented(
				$"Connecting to {provider} is not implemented yet.");
		}

		[HttpGet("RemoveApp")]
		public IActionResult RemoveApp(
			[FromQuery] string id)
		{
			return NotImplemented(
				$"Removing application {id} is not implemented yet.");
		}

		private async Task<IActionResult> DeleteUploadCore(
			string path)
		{
			var me = await Me();

			if (me == null)
				return Unauthorized(new { message = "Authentication required." });

			if (!TryResolveOwnedUpload(
					me.Id,
					path,
					out var fullPath))
			{
				return BadRequest(new
				{
					ok = false,
					message = "Invalid avatar path."
				});
			}

			if (!System.IO.File.Exists(fullPath))
			{
				return NotFound(new
				{
					ok = false,
					message = "Avatar file was not found."
				});
			}

			System.IO.File.Delete(fullPath);

			_ui.UnregisterUserUploadedAvatar(me.Id, path);

			if (string.Equals(
					me.AvatarPath,
					path,
					StringComparison.OrdinalIgnoreCase))
			{
				me.AvatarPath = null;

				_db.Update(me);
				await _db.SaveChangesAsync();
			}

			return Ok(new { ok = true });
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
				return false;

			var expectedPrefix =
				$"/uploads/avatars/{userId}/";

			if (!webPath.StartsWith(
					expectedPrefix,
					StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			try
			{
				var fileName = Path.GetFileName(webPath);

				if (string.IsNullOrWhiteSpace(fileName))
					return false;

				var avatarDirectory = Path.GetFullPath(
					GetAvatarDirectory(userId));

				var candidate = Path.GetFullPath(
					Path.Combine(avatarDirectory, fileName));

				if (!candidate.StartsWith(
						avatarDirectory + Path.DirectorySeparatorChar,
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

		private IActionResult NotImplemented(string message)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new { message });
		}
	}
}
