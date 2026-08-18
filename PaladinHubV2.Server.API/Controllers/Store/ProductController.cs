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
		private readonly IProductService _products;

		public ProductsController(IProductService products)
		{
			_products = products;
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
		public async Task<IActionResult> Categories(
			CancellationToken cancellationToken = default)
		{
			var categories =
				await _products.GetAllCategoriesAsync(
					cancellationToken);

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
				User.FindFirstValue(ClaimTypes.NameIdentifier);

			bool isAdmin = User.IsInRole("Admin");

			var model =
				await _products.GetDetailsAsync(
					id.Trim(),
					userId,
					isAdmin,
					cancellationToken);

			return model == null
				? NotFound(new
				{
					message = "Product not found."
				})
				: Ok(model);
		}
	}
}
