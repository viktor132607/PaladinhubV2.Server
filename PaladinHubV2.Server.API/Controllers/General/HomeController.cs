using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.General
{
	[ApiController]
	[Route("api/home")]
	public sealed class HomeController : ControllerBase
	{
		[AllowAnonymous]
		[HttpGet]
		[HttpGet("home")]
		[HttpGet("~/Home/Home")]
		public IActionResult Home()
		{
			return Ok(new
			{
				page = "home",
				frontendRoute = "/Home/Home"
			});
		}

		[AllowAnonymous]
		[HttpGet("privacy")]
		[HttpGet("~/Home/Privacy")]
		public IActionResult Privacy()
		{
			return Ok(new
			{
				page = "privacy",
				frontendRoute = "/Home/Privacy"
			});
		}

		[AllowAnonymous]
		[HttpGet("discussion")]
		[HttpGet("~/Home/Discussion")]
		public IActionResult Discussion()
		{
			return Ok(new
			{
				redirectUrl = "/Discussions/Index"
			});
		}
	}
}
