using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

		private Task<User?> Me()
		{
			return _ui.GetMe(User);
		}

		[HttpGet("Security")]
		public async Task<IActionResult> Security()
		{
			var me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var requireTwoFactor =
				HttpContext.Session.GetString("require_2fa") == "1";

			var recoveryCodesLeft =
				await _userManager.CountRecoveryCodesAsync(me);

			var (securityScore, securityTips) =
				_ui.ComputeSecurityScore(me);

			var device = Request.Headers["User-Agent"].ToString();

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

		[HttpGet("Enable2FA")]
		public async Task<IActionResult> Enable2FA(
			[FromQuery] bool reset = false)
		{
			var me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (me.TwoFactorEnabled)
			{
				if (reset)
				{
					return Conflict(new
					{
						message =
							"Disable two-factor authentication before resetting the authenticator key."
					});
				}

				return Ok(new
				{
					twoFactorEnabled = true,
					sharedKey = (string?)null,
					authenticatorUri = (string?)null,
					qrCodeUrl = (string?)null
				});
			}

			var key = await _userManager.GetAuthenticatorKeyAsync(me);

			if (reset || string.IsNullOrWhiteSpace(key))
			{
				var resetResult =
					await _userManager.ResetAuthenticatorKeyAsync(me);

				if (!resetResult.Succeeded)
				{
					return StatusCode(
						StatusCodes.Status500InternalServerError,
						new
						{
							message =
								"Authenticator key could not be generated.",
							errors = resetResult.Errors
								.Select(error => error.Description)
								.ToArray()
						});
				}

				key = await _userManager.GetAuthenticatorKeyAsync(me);
			}

			if (string.IsNullOrWhiteSpace(key))
			{
				return StatusCode(
					StatusCodes.Status500InternalServerError,
					new
					{
						message =
							"Authenticator key could not be loaded."
					});
			}

			var issuer = Uri.EscapeDataString("PaladinHub");

			var accountName = Uri.EscapeDataString(
				me.Email ??
				me.UserName ??
				me.Id);

			var authenticatorUri =
				$"otpauth://totp/{issuer}:{accountName}" +
				$"?secret={key}" +
				$"&issuer={issuer}" +
				"&digits=6" +
				"&algorithm=SHA1" +
				"&period=30";

			var qrCodeUrl =
				"https://api.qrserver.com/v1/create-qr-code/" +
				"?size=180x180&data=" +
				Uri.EscapeDataString(authenticatorUri);

			return Ok(new
			{
				twoFactorEnabled = false,
				sharedKey = FormatKey(key),
				authenticatorUri,
				qrCodeUrl
			});
		}

		[HttpPost("Enable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Enable2FA(
			[FromForm] string code)
		{
			var me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (me.TwoFactorEnabled)
			{
				return Conflict(new
				{
					message =
						"Two-factor authentication is already enabled."
				});
			}

			var sanitizedCode = Regex.Replace(
				code ?? string.Empty,
				"[^0-9]",
				string.Empty);

			if (sanitizedCode.Length != 6)
			{
				return BadRequest(new
				{
					message =
						"Enter a valid 6-digit authenticator code."
				});
			}

			var valid =
				await _userManager.VerifyTwoFactorTokenAsync(
					me,
					TokenOptions.DefaultAuthenticatorProvider,
					sanitizedCode);

			if (!valid)
			{
				return BadRequest(new
				{
					message = "Invalid authenticator code."
				});
			}

			await _security.ToggleTwoFactor(me, true);

			var generatedCodes =
				await _userManager
					.GenerateNewTwoFactorRecoveryCodesAsync(
						me,
						10);

			var recoveryCodes =
				generatedCodes?.ToArray() ??
				Array.Empty<string>();

			return Ok(new
			{
				ok = true,
				message =
					"Two-factor authentication enabled.",
				twoFactorEnabled = true,
				recoveryCodes
			});
		}

		[HttpPost("Disable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Disable2FA()
		{
			var me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (me.TwoFactorEnabled)
			{
				await _security.ToggleTwoFactor(me, false);
			}

			HttpContext.Session.SetString(
				"require_2fa",
				"0");

			return Ok(new
			{
				ok = true,
				message =
					"Two-factor authentication disabled.",
				twoFactorEnabled = false,
				requireTwoFactor = false
			});
		}

		[HttpPost("GenerateRecoveryCode")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> GenerateRecoveryCode()
		{
			var me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (!me.TwoFactorEnabled)
			{
				return Conflict(new
				{
					message =
						"Enable two-factor authentication before generating recovery codes."
				});
			}

			var generatedCodes =
				await _userManager
					.GenerateNewTwoFactorRecoveryCodesAsync(
						me,
						10);

			var recoveryCodes =
				generatedCodes?.ToArray() ??
				Array.Empty<string>();

			return Ok(new
			{
				ok = true,
				message =
					"New recovery codes generated.",
				recoveryCodes
			});
		}

		[HttpPost("ToggleRequire2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleRequire2FA(
			[FromForm] bool on = false)
		{
			var me = await Me();

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
			var me = await Me();

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

		private static string FormatKey(string key)
		{
			var normalizedKey =
				key.ToUpperInvariant();

			return Regex.Replace(
				normalizedKey,
				".{4}",
				"$0 ")
				.Trim();
		}
	}
}
