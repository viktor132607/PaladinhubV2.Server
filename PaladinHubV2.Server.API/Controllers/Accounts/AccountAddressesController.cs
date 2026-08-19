using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountAddressesController : ControllerBase
	{
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
