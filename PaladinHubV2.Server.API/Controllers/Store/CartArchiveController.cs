using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartArchiveController : ControllerBase
	{
		private readonly ICartService _cartService;

		public CartArchiveController(ICartService cartService)
		{
			_cartService = cartService;
		}

		[HttpGet("archive")]
		[HttpGet("Archive")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Archive()
		{
			return Ok(await _cartService.GetArchivedOrders());
		}

		[HttpGet("archive/{id:guid}")]
		[HttpGet("Details/{id:guid}")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Details([FromRoute] Guid id)
		{
			if (id == Guid.Empty)
				return BadRequest(new { message = "Invalid cart ID." });

			var order = await _cartService.GetArchivedOrder(id);
			if (order is null)
				return NotFound(new { message = "Archived cart not found." });

			return Ok(order);
		}

		[HttpPut("archive/{id:guid}/status")]
		[HttpPut("Archive/{id:guid}/Status")]
		public async Task<IActionResult> UpdateStatus(
			[FromRoute] Guid id,
			[FromBody] UpdateOrderStatusRequest request)
		{
			if (id == Guid.Empty)
				return BadRequest(new { message = "Invalid cart ID." });

			if (!OrderStatusCatalog.TryNormalize(request.Status, out string normalizedStatus))
			{
				return BadRequest(new
				{
					message = "Invalid order status.",
					allowedStatuses = OrderStatusCatalog.All
				});
			}

			bool updated = await _cartService.UpdateOrderStatus(id, normalizedStatus);
			if (!updated)
				return NotFound(new { message = "Archived cart not found." });

			return Ok(new { id, status = normalizedStatus });
		}

		public sealed record UpdateOrderStatusRequest(string Status);
	}
}
