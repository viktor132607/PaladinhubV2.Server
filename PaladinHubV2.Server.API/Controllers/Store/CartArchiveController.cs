using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
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

		[Authorize(Roles = "Admin")]
		[HttpGet("archive")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Archive()
		{
			var legacyCarts = await _cartService.GetArchive();
			var orders = await _cartService.GetArchivedOrders();

			if (orders is not null)
			{
				return Ok(orders);
			}

			var response = legacyCarts.Select(cart => new
			{
				id = cart.Id,
				username = cart.User?.UserName ?? "Unknown",
				orderDate = cart.OrderDate ?? string.Empty,
				status = OrderStatusCatalog.Pending
			});

			return Ok(response);
		}

		[Authorize(Roles = "Admin")]
		[HttpGet("archive/{id:guid}")]
		[HttpGet("Details/{id:guid}")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Details([FromRoute] Guid id)
		{
			if (id == Guid.Empty)
				return BadRequest(new { message = "Invalid cart ID." });

			var cart = await _cartService.GetCartById(id);
			if (cart is null)
				return NotFound(new { message = "Archived cart not found." });

			var order = await _cartService.GetArchivedOrder(id);
			return Ok(order is null ? cart : order);
		}

		[HttpPut("archive/{id:guid}/status")]
		public async Task<IActionResult> UpdateStatus(
			[FromRoute] Guid id,
			[FromBody] UpdateOrderStatusRequest request)
		{
			if (!User.IsInRole("Admin"))
				return Forbid();

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
