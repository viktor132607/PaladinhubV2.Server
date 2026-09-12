using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductsController : ControllerBase
	{
		private readonly IProductService _productService;

		public ProductsController(
			IProductService productService)
		{
			_productService = productService;
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Index()
		{
			string target =
				"/Merchandise/List" +
				Request.QueryString;

			return Redirect(target);
		}

		[AllowAnonymous]
		[HttpGet("categories")]
		public async Task<IActionResult> Categories()
		{
			List<string> categories =
				await _productService.GetCategories();

			return Ok(categories);
		}

		[AllowAnonymous]
		[HttpGet("{id}")]
		public Task<IActionResult> DetailsApi(
			[FromRoute] string id,
			CancellationToken cancellationToken)
		{
			return DetailsCore(id, cancellationToken);
		}

		[AllowAnonymous]
		[HttpGet("Details")]
		public Task<IActionResult> DetailsLegacy(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			return DetailsCore(id, cancellationToken);
		}

		private async Task<IActionResult> DetailsCore(
			string? id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			string? userId =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

			bool isAdmin =
				User.IsInRole("Admin");

			var model =
				await _productService.GetDetailsAsync(
					id.Trim(),
					userId,
					isAdmin,
					cancellationToken);

			if (model == null)
			{
				return NotFound(new
				{
					message = "Product not found."
				});
			}

			return Ok(model);
		}
	}
}
