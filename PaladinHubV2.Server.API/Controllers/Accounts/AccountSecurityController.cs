using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountSecurityController : ControllerBase
	{
		private readonly ISecurityService _security;
		private readonly IAccountUiService _ui;
		private readonly UserManager<User> _userManager;

		public AccountSecurityController(
			ISecurityService security,
			IAccountUiService ui,
			UserManager<User> userManager)
		{
			_security = security;
			_ui = ui;
			_userManager = userManager;
		}

		[HttpGet("Security")]
		public async Task<IActionResult> Security()
		{
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			bool requireTwoFactor =
				HttpContext.Session.GetString("require_2fa") == "1";

			int recoveryCodesLeft =
				await _userManager.CountRecoveryCodesAsync(me);

			var (securityScore, securityTips) =
				_ui.ComputeSecurityScore(me);

			string device = Request.Headers["User-Agent"].ToString();

			return Ok(new
			{
				twoFactorEnabled = me.TwoFactorEnabled,
				requireTwoFactor,
				recoveryCodesLeft,
				phoneNumber = me.PhoneNumber,
				phoneNumberConfirmed = me.PhoneNumberConfirmed,
				email = me.Email,
				emailConfirmed = me.EmailConfirmed,
				passwordChangedAt = (DateTime?)null,
				securityScore,
				securityTips,
				lastLogin = new
				{
					when = "Just now",
					where = "Website",
					device
				}
			});
		}

		[HttpPost("ToggleRequire2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleRequire2FA(
			[FromForm] bool on = false)
		{
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (on && !me.TwoFactorEnabled)
			{
				return Conflict(new
				{
					message =
						"Enable two-factor authentication first."
				});
			}

			HttpContext.Session.SetString(
				"require_2fa",
				on ? "1" : "0");

			return Ok(new
			{
				ok = true,
				message = on
					? "Authenticator required for login is ON."
					: "Authenticator required for login is OFF.",
				requireTwoFactor = on
			});
		}

		[HttpPost("LogoutAllDevices")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LogoutAllDevices()
		{
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			await _security.LogoutAllDevices(me);

			return Ok(new
			{
				ok = true,
				message = "Logged out from all devices."
			});
		}

		private Task<User?> Me()
		{
			return _ui.GetMe(User);
		}
	}
}
