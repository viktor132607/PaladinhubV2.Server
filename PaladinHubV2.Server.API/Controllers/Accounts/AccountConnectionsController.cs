using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountConnectionsController : ControllerBase
	{
		[HttpGet("Connections")]
		public IActionResult Connections() => NoContent();

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
