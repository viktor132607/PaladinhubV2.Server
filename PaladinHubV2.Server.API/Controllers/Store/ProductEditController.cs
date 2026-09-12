using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductEditController : ControllerBase
	{
		private readonly IProductService _productService;
		private readonly IProductAdminFormService _formService;

		public ProductEditController(
			IProductService productService,
			IProductAdminFormService formService)
		{
			_productService = productService;
			_formService = formService;
		}

		[HttpGet("{id}/edit")]
		public Task<IActionResult> EditApi(
			[FromRoute] string id,
			CancellationToken cancellationToken)
		{
			return EditGetCore(id, cancellationToken);
		}

		[HttpGet("Edit")]
		public Task<IActionResult> EditLegacy(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			return EditGetCore(id, cancellationToken);
		}

		[HttpPut("{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> EditApi(
			[FromRoute] string id,
			[FromBody] EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model != null &&
				!string.Equals(id, model.Id, StringComparison.Ordinal))
			{
				return Task.FromResult<IActionResult>(
					BadRequest(new
					{
						message = "The route product ID does not match the request product ID."
					}));
			}

			return EditCore(model, cancellationToken);
		}

		[HttpPost("Edit")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> EditLegacy(
			[FromForm] EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return EditCore(model, cancellationToken);
		}

		private async Task<IActionResult> EditGetCore(
			string? id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new { message = "Product ID is required." });
			}

			EditProductViewModel? model =
				await _formService.BuildEditModelAsync(
					id.Trim(),
					cancellationToken);

			if (model == null)
			{
				return NotFound(new { message = "Product not found." });
			}

			return Ok(model);
		}

		private async Task<IActionResult> EditCore(
			EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new { message = "Product data is required." });
			}

			if (string.IsNullOrWhiteSpace(model.Id))
			{
				return BadRequest(new { message = "Product ID is required." });
			}

			_formService.ApplyNewCategory(model);
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			bool updated = await _productService.UpdateAsync(
				model,
				cancellationToken);

			if (!updated)
			{
				return Conflict(new
				{
					message = "Product was not found or another product already uses this name."
				});
			}

			return Ok(new
			{
				ok = true,
				id = model.Id,
				message = "Product updated successfully."
			});
		}
	}
}
