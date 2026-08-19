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
	public sealed class AccountContactController : ControllerBase
	{
		private readonly IAccountUiService _ui;
		private readonly ISecurityService _security;

		public AccountContactController(
			IAccountUiService ui,
			ISecurityService security)
		{
			_ui = ui;
			_security = security;
		}

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

		private IActionResult NotImplemented(string message)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new { message });
		}
	}
}
