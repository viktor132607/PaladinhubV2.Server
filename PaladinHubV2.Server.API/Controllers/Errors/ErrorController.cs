using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Errors
{
	[ApiController]
	[AllowAnonymous]
	[Route("error")]
	public sealed class ErrorController : ControllerBase
	{
		[HttpGet("404")]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public IActionResult NotFound404()
		{
			return Problem(
				statusCode: StatusCodes.Status404NotFound,
				title: "Resource not found",
				detail: "The requested resource could not be found.",
				instance: HttpContext.Request.Path);
		}

		[HttpGet("500")]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
		public IActionResult InternalServerError()
		{
			return Problem(
				statusCode: StatusCodes.Status500InternalServerError,
				title: "Internal server error",
				detail: "An unexpected server error occurred.",
				instance: HttpContext.Request.Path);
		}
	}
}
