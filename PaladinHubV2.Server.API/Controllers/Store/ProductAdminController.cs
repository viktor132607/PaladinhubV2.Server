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
	public sealed class ProductAdminController : ControllerBase
	{
		private readonly IProductAdminService _admin;

		public ProductAdminController(IProductAdminService admin)
		{
			_admin = admin;
		}

		[HttpGet("Create")]
		public async Task<IActionResult> Create(
			CancellationToken cancellationToken = default)
		{
			var model =
				await _admin.BuildCreateModelAsync(cancellationToken);

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
				!string.Equals(
					id,
					model.Id,
					StringComparison.Ordinal))
			{
				return Task.FromResult<IActionResult>(
					BadRequest(new
					{
						message =
							"The route product ID does not match the request product ID."
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

		[HttpDelete("{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteApi(
			[FromRoute] string id)
		{
			return DeleteCore(id);
		}

		[HttpGet("DeleteProduct")]
		public Task<IActionResult> DeleteLegacy(
			[FromQuery] string id)
		{
			return DeleteCore(id);
		}

		private async Task<IActionResult> CreateCore(
			CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new
				{
					message = "Product data is required."
				});
			}

			_admin.Normalize(model);

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var created =
				await _admin.CreateAsync(
					model,
					cancellationToken);

			if (created == null)
			{
				return Conflict(new
				{
					message =
						"Product with this name already exists."
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

		private async Task<IActionResult> EditGetCore(
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

			var model =
				await _admin.GetForEditAsync(
					id.Trim(),
					cancellationToken);

			return model == null
				? NotFound(new
				{
					message = "Product not found."
				})
				: Ok(model);
		}

		private async Task<IActionResult> EditCore(
			EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new
				{
					message = "Product data is required."
				});
			}

			if (string.IsNullOrWhiteSpace(model.Id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			_admin.Normalize(model);

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			bool updated =
				await _admin.UpdateAsync(
					model,
					cancellationToken);

			if (!updated)
			{
				return Conflict(new
				{
					message =
						"Product was not found or another product already uses this name."
				});
			}

			return Ok(new
			{
				ok = true,
				id = model.Id,
				message = "Product updated successfully."
			});
		}

		private async Task<IActionResult> DeleteCore(string? id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			bool deleted = await _admin.DeleteAsync(id.Trim());

			return deleted
				? NoContent()
				: NotFound(new
				{
					message = "Product not found."
				});
		}
	}
}
