using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductCreateController : ControllerBase
	{
		private readonly IProductService _productService;
		private readonly IProductAdminFormService _formService;

		public ProductCreateController(
			IProductService productService,
			IProductAdminFormService formService)
		{
			_productService = productService;
			_formService = formService;
		}

		[HttpGet("Create")]
		public async Task<IActionResult> Create()
		{
			CreateProductViewModel model =
				await _formService.BuildCreateModelAsync();
			return Ok(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateApi(
			[FromBody] CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		[HttpPost("Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateLegacy(
			[FromForm] CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		private async Task<IActionResult> CreateCore(
			CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new { message = "Product data is required." });
			}

			_formService.ApplyNewCategory(model);
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			CreateProductViewModel created =
				await _productService.Create(model);

			if (created == null)
			{
				return Conflict(new
				{
					message = "Product with this name already exists."
				});
			}

			return StatusCode(
				StatusCodes.Status201Created,
				new
				{
					ok = true,
					product = created
				});
		}
	}
}
