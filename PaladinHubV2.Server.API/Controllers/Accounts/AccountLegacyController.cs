using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountLegacyController : ControllerBase
	{
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

		private IActionResult NotImplemented(string message)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new { message });
		}
	}
}
