using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.General
{
	[ApiController]
	[Route("api/home")]
	public sealed class HomeController : ControllerBase
	{
		private readonly IProductService _productService;

		public HomeController(IProductService productService)
		{
			_productService = productService;
		}

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
		[HttpGet("merchandise")]
		[HttpGet("~/Home/Merchandise")]
		public async Task<IActionResult> Merchandise()
		{
			ICollection<ProductViewModel> products =
				await _productService.GetAll();

			return Ok(products);
		}

		[Authorize]
		[HttpGet("thanks-for-purchasing")]
		[HttpGet("~/Home/ThanksForPurchasing")]
		public IActionResult ThanksForPurchasing()
		{
			return Ok(new
			{
				message = "Thank you for your purchase.",
				frontendRoute = "/checkout/ThanksForPurchasing"
			});
		}

		[Authorize]
		[HttpGet("logged-in-products")]
		[HttpGet("~/Home/IndexLoggedIn")]
		public async Task<IActionResult> IndexLoggedIn()
		{
			ICollection<ProductViewModel> products =
				await _productService.GetAll();

			return Ok(products);
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

		[AllowAnonymous]
		[HttpGet("error")]
		[HttpGet("~/Home/Error")]
		[ResponseCache(
			Duration = 0,
			Location = ResponseCacheLocation.None,
			NoStore = true)]
		public IActionResult Error()
		{
			return Problem(
				statusCode: StatusCodes.Status500InternalServerError,
				title: "Internal server error",
				detail: "An unexpected server error occurred.",
				instance: HttpContext.Request.Path,
				extensions: new Dictionary<string, object?>
				{
					["requestId"] = HttpContext.TraceIdentifier
				});
		}
	}
}
