using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountSecurityController : ControllerBase
	{
		private readonly IAccountSecurityApplicationService _security;

		public AccountSecurityController(IAccountSecurityApplicationService security)
		{
			_security = security;
		}

		[HttpGet("Security")]
		public async Task<IActionResult> Security()
		{
			bool requireTwoFactor =
				HttpContext.Session.GetString("require_2fa") == "1";
			string device = Request.Headers["User-Agent"].ToString();
			return ToActionResult(await _security.GetOverviewAsync(
				User,
				requireTwoFactor,
				device));
		}

		[HttpGet("Enable2FA")]
		public async Task<IActionResult> Enable2FA([FromQuery] bool reset = false)
			=> ToActionResult(await _security.GetEnableTwoFactorAsync(User, reset));

		[HttpPost("Enable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Enable2FA([FromForm] string code)
			=> ToActionResult(await _security.EnableTwoFactorAsync(User, code));

		[HttpPost("Disable2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Disable2FA()
		{
			AccountSecurityResult result = await _security.DisableTwoFactorAsync(User);
			if (result.Status == AccountSecurityStatus.Ok)
			{
				HttpContext.Session.SetString("require_2fa", "0");
			}
			return ToActionResult(result);
		}

		[HttpPost("GenerateRecoveryCode")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> GenerateRecoveryCode()
			=> ToActionResult(await _security.GenerateRecoveryCodesAsync(User));

		[HttpPost("ToggleRequire2FA")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleRequire2FA([FromForm] bool on = false)
		{
			AccountSecurityResult result =
				await _security.ValidateRequireTwoFactorAsync(User, on);
			if (result.Status == AccountSecurityStatus.Ok)
			{
				HttpContext.Session.SetString("require_2fa", on ? "1" : "0");
			}
			return ToActionResult(result);
		}

		[HttpPost("LogoutAllDevices")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LogoutAllDevices()
			=> ToActionResult(await _security.LogoutAllDevicesAsync(User));

		private IActionResult ToActionResult(AccountSecurityResult result)
		{
			return result.Status switch
			{
				AccountSecurityStatus.Ok => Ok(result.Payload),
				AccountSecurityStatus.BadRequest => BadRequest(result.Payload),
				AccountSecurityStatus.Unauthorized => Unauthorized(result.Payload),
				AccountSecurityStatus.Conflict => Conflict(result.Payload),
				AccountSecurityStatus.InternalError => StatusCode(
					StatusCodes.Status500InternalServerError,
					result.Payload),
				_ => StatusCode(StatusCodes.Status500InternalServerError, result.Payload)
			};
		}
	}
}
