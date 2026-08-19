using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountPreferencesController : ControllerBase
	{
		[HttpGet("Settings")]
		public IActionResult Settings() => NoContent();

		[HttpGet("Privacy")]
		public IActionResult Privacy() => NoContent();
	}
}
