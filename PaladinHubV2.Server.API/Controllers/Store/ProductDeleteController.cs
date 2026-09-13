using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductDeleteController : ControllerBase
	{
		private readonly IProductService _productService;

		public ProductDeleteController(IProductService productService)
		{
			_productService = productService;
		}

		[HttpDelete("{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteApi([FromRoute] string id)
		{
			return DeleteCore(id);
		}

		[HttpGet("DeleteProduct")]
		public IActionResult DeleteLegacy([FromQuery] string id)
		{
			_ = id;
			return StatusCode(
				StatusCodes.Status405MethodNotAllowed,
				new
				{
					message = "Legacy product deletion by GET is disabled. Use DELETE /api/products/{id}."
				});
		}

		private async Task<IActionResult> DeleteCore(string? id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new { message = "Product ID is required." });
			}

			bool deleted = await _productService.Delete(id.Trim());
			if (!deleted)
			{
				return NotFound(new { message = "Product not found." });
			}

			return NoContent();
		}
	}
}
