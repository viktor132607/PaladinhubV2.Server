using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountProfileController : ControllerBase
	{
		[HttpGet("AccountDetails")]
		public IActionResult AccountDetails() => NoContent();

		[HttpGet("EditProfile")]
		public IActionResult EditProfile() =>
			NotImplemented(
				"Profile editing is not implemented yet.");

		[HttpGet("EditBattleTag")]
		public IActionResult EditBattleTag() =>
			NotImplemented(
				"BattleTag change is not supported.");

		private IActionResult NotImplemented(string message)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new { message });
		}
	}
}
