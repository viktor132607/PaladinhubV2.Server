using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountApplicationsController : ControllerBase
	{
		[HttpGet("RemoveApp")]
		public IActionResult RemoveApp(
			[FromQuery] string id)
		{
			return StatusCode(
				StatusCodes.Status501NotImplemented,
				new
				{
					message =
						$"Removing application {id} is not implemented yet."
				});
		}
	}
}
