using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountSecurityApplicationService : IAccountSecurityApplicationService
	{
		private readonly ISecurityService _security;
		private readonly IAccountUiService _ui;
		private readonly UserManager<User> _userManager;

		public AccountSecurityApplicationService(
			ISecurityService security,
			IAccountUiService ui,
			UserManager<User> userManager)
		{
			_security = security;
			_ui = ui;
			_userManager = userManager;
		}

		public async Task<AccountSecurityResult> GetOverviewAsync(
			ClaimsPrincipal principal,
			bool requireTwoFactor,
			string device)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();

			int recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(me);
			var (securityScore, securityTips) = _ui.ComputeSecurityScore(me);

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

		public async Task<AccountSecurityResult> GetEnableTwoFactorAsync(
			ClaimsPrincipal principal,
			bool reset)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();

			if (me.TwoFactorEnabled)
			{
				if (reset)
				{
					return Conflict(new
					{
						message = "Disable two-factor authentication before resetting the authenticator key."
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

			string? key = await _userManager.GetAuthenticatorKeyAsync(me);
			if (reset || string.IsNullOrWhiteSpace(key))
			{
				IdentityResult resetResult = await _userManager.ResetAuthenticatorKeyAsync(me);
				if (!resetResult.Succeeded)
				{
					return InternalError(new
					{
						message = "Authenticator key could not be generated.",
						errors = resetResult.Errors.Select(error => error.Description).ToArray()
					});
				}
				key = await _userManager.GetAuthenticatorKeyAsync(me);
			}

			if (string.IsNullOrWhiteSpace(key))
				return InternalError(new { message = "Authenticator key could not be loaded." });

			string issuer = Uri.EscapeDataString("PaladinHub");
			string accountName = Uri.EscapeDataString(me.Email ?? me.UserName ?? me.Id);
			string authenticatorUri =
				$"otpauth://totp/{issuer}:{accountName}" +
				$"?secret={key}" +
				$"&issuer={issuer}" +
				"&digits=6" +
				"&algorithm=SHA1" +
				"&period=30";
			string qrCodeUrl =
				"https://api.qrserver.com/v1/create-qr-code/" +
				"?size=180x180&data=" + Uri.EscapeDataString(authenticatorUri);

			return Ok(new
			{
				twoFactorEnabled = false,
				sharedKey = FormatKey(key),
				authenticatorUri,
				qrCodeUrl
			});
		}

		public async Task<AccountSecurityResult> EnableTwoFactorAsync(
			ClaimsPrincipal principal,
			string? code)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();
			if (me.TwoFactorEnabled)
				return Conflict(new { message = "Two-factor authentication is already enabled." });

			string sanitizedCode = Regex.Replace(code ?? string.Empty, "[^0-9]", string.Empty);
			if (sanitizedCode.Length != 6)
				return BadRequest(new { message = "Enter a valid 6-digit authenticator code." });

			bool valid = await _userManager.VerifyTwoFactorTokenAsync(
				me,
				TokenOptions.DefaultAuthenticatorProvider,
				sanitizedCode);
			if (!valid)
				return BadRequest(new { message = "Invalid authenticator code." });

			await _security.ToggleTwoFactor(me, true);
			var generatedCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(me, 10);
			string[] recoveryCodes = generatedCodes?.ToArray() ?? Array.Empty<string>();

			return Ok(new
			{
				ok = true,
				message = "Two-factor authentication enabled.",
				twoFactorEnabled = true,
				recoveryCodes
			});
		}

		public async Task<AccountSecurityResult> DisableTwoFactorAsync(ClaimsPrincipal principal)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();
			if (me.TwoFactorEnabled)
				await _security.ToggleTwoFactor(me, false);

			return Ok(new
			{
				ok = true,
				message = "Two-factor authentication disabled.",
				twoFactorEnabled = false,
				requireTwoFactor = false
			});
		}

		public async Task<AccountSecurityResult> GenerateRecoveryCodesAsync(ClaimsPrincipal principal)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();
			if (!me.TwoFactorEnabled)
			{
				return Conflict(new
				{
					message = "Enable two-factor authentication before generating recovery codes."
				});
			}

			var generatedCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(me, 10);
			string[] recoveryCodes = generatedCodes?.ToArray() ?? Array.Empty<string>();
			return Ok(new
			{
				ok = true,
				message = "New recovery codes generated.",
				recoveryCodes
			});
		}

		public async Task<AccountSecurityResult> ValidateRequireTwoFactorAsync(
			ClaimsPrincipal principal,
			bool on)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();
			if (on && !me.TwoFactorEnabled)
				return Conflict(new { message = "Enable two-factor authentication first." });

			return Ok(new
			{
				ok = true,
				message = on
					? "Authenticator required for login is ON."
					: "Authenticator required for login is OFF.",
				requireTwoFactor = on
			});
		}

		public async Task<AccountSecurityResult> LogoutAllDevicesAsync(ClaimsPrincipal principal)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null) return Unauthorized();
			await _security.LogoutAllDevices(me);
			return Ok(new { ok = true, message = "Logged out from all devices." });
		}

		private static string FormatKey(string key)
		{
			string normalizedKey = key.ToUpperInvariant();
			return Regex.Replace(normalizedKey, ".{4}", "$0 ").Trim();
		}

		private static AccountSecurityResult Ok(object payload) => new(AccountSecurityStatus.Ok, payload);
		private static AccountSecurityResult BadRequest(object payload) => new(AccountSecurityStatus.BadRequest, payload);
		private static AccountSecurityResult Conflict(object payload) => new(AccountSecurityStatus.Conflict, payload);
		private static AccountSecurityResult InternalError(object payload) => new(AccountSecurityStatus.InternalError, payload);
		private static AccountSecurityResult Unauthorized() => new(
			AccountSecurityStatus.Unauthorized,
			new { message = "Authentication required." });
	}
}
