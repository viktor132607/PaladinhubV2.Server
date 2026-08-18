using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountProfileController : ControllerBase
	{
		private readonly IAccountUiService _ui;
		private readonly ISecurityService _security;

		public AccountProfileController(
			IAccountUiService ui,
			ISecurityService security)
		{
			_ui = ui;
			_security = security;
		}

		[HttpGet("Settings")]
		public IActionResult Settings() => NoContent();

		[HttpGet("AccountDetails")]
		public IActionResult AccountDetails() => NoContent();

		[HttpGet("Privacy")]
		public IActionResult Privacy() => NoContent();

		[HttpPost("MarkPhoneVerified")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkPhoneVerified()
		{
			User? me = await _ui.GetMe(User);
			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			await _security.MarkPhoneVerified(me);

			return Ok(new
			{
				ok = true,
				phoneNumberConfirmed = true
			});
		}

		[HttpGet("EditProfile")]
		public IActionResult EditProfile() =>
			NotImplemented(
				"Profile editing is not implemented yet.");

		[HttpGet("EditEmail")]
		public IActionResult EditEmail() =>
			NotImplemented(
				"Email change is not implemented yet.");

		[HttpGet("EditPhone")]
		public IActionResult EditPhone() =>
			NotImplemented(
				"Phone update is not implemented yet.");

		[HttpGet("RemovePhone")]
		public IActionResult RemovePhone() =>
			NotImplemented(
				"Phone removal is not implemented yet.");

		[HttpGet("EditBattleTag")]
		public IActionResult EditBattleTag() =>
			NotImplemented(
				"BattleTag change is not supported.");

		[HttpGet("AddAddress")]
		public IActionResult AddAddress() =>
			NotImplemented(
				"Address creation is not implemented yet.");

		[HttpGet("EditAddress")]
		public IActionResult EditAddress() =>
			NotImplemented(
				"Address editing is not implemented yet.");

		private IActionResult NotImplemented(string message)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new { message });
		}
	}
}
