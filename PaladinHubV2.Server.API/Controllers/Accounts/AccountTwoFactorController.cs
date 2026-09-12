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
	public sealed class AccountTwoFactorController : ControllerBase
	{
		private readonly ISecurityService _security;
		private readonly IAccountUiService _ui;
		private readonly AccountTwoFactorService _twoFactor;

		public AccountTwoFactorController(
			ISecurityService security,
			IAccountUiService ui,
			UserManager<User> userManager)
		{
			_security = security;
			_ui = ui;
			_twoFactor = new AccountTwoFactorService(
				security,
				userManager);
		}

		[HttpGet("Enable2FA")]
		public async Task<IActionResult> Enable2FA(
			[FromQuery] bool reset = false)
		{
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			TwoFactorSetupResult result =
				await _twoFactor.PrepareSetupAsync(me, reset);

			if (result.Error ==
				TwoFactorSetupError.MustDisableBeforeReset)
			{
				return Conflict(new
				{
					message =
						"Disable two-factor authentication before resetting the authenticator key."
				});
			}

			if (result.Error ==
				TwoFactorSetupError.KeyGenerationFailed)
			{
				return StatusCode(
					StatusCodes.Status500InternalServerError,
					new
					{
						message =
							"Authenticator key could not be generated.",
						errors = result.Errors ?? Array.Empty<string>()
					});
			}

			if (result.Error ==
				TwoFactorSetupError.KeyLoadFailed)
			{
				return StatusCode(
					StatusCodes.Status500InternalServerError,
					new
					{
						message =
							"Authenticator key could not be loaded."
					});
			}

			return Ok(new
			{
				twoFactorEnabled = result.TwoFactorEnabled,
				sharedKey = result.SharedKey,
				authenticatorUri = result.AuthenticatorUri,
				qrCodeUrl = result.QrCodeUrl
			});
		}

		[HttpPost("Enable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Enable2FA(
			[FromForm] string code)
		{
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			TwoFactorEnableResult result =
				await _twoFactor.EnableAsync(me, code);

			if (result.Error ==
				TwoFactorEnableError.AlreadyEnabled)
			{
				return Conflict(new
				{
					message =
						"Two-factor authentication is already enabled."
				});
			}

			if (result.Error ==
				TwoFactorEnableError.InvalidCodeFormat)
			{
				return BadRequest(new
				{
					message =
						"Enter a valid 6-digit authenticator code."
				});
			}

			if (result.Error ==
				TwoFactorEnableError.InvalidCode)
			{
				return BadRequest(new
				{
					message = "Invalid authenticator code."
				});
			}

			return Ok(new
			{
				ok = true,
				message =
					"Two-factor authentication enabled.",
				twoFactorEnabled = true,
				recoveryCodes = result.RecoveryCodes
			});
		}

		[HttpPost("Disable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Disable2FA()
		{
			User? me = await Me();

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
			User? me = await Me();

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			string[]? recoveryCodes =
				await _twoFactor.GenerateRecoveryCodesAsync(me);

			if (recoveryCodes == null)
			{
				return Conflict(new
				{
					message =
						"Enable two-factor authentication before generating recovery codes."
				});
			}

			return Ok(new
			{
				ok = true,
				message =
					"New recovery codes generated.",
				recoveryCodes
			});
		}

		private Task<User?> Me()
		{
			return _ui.GetMe(User);
		}
	}
}
