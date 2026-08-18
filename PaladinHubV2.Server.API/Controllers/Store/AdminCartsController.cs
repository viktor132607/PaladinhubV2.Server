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
	public sealed class AdminCartsController : ControllerBase
	{
		private readonly ICartService _cartService;

		public AdminCartsController(ICartService cartService)
		{
			_cartService = cartService;
		}

		[HttpGet("archive")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Archive()
		{
			var archivedCarts = await _cartService.GetArchive();

			return Ok(archivedCarts.Select(cart => new
			{
				id = cart.Id,
				username = cart.User?.UserName ?? "Unknown",
				orderDate = cart.OrderDate ?? string.Empty
			}));
		}

		[HttpGet("archive/{id:guid}")]
		[HttpGet("Details/{id:guid}")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Details([FromRoute] Guid id)
		{
			if (id == Guid.Empty)
			{
				return BadRequest(new { message = "Invalid cart ID." });
			}

			var cart = await _cartService.GetCartById(id);

			return cart == null
				? NotFound(new { message = "Archived cart not found." })
				: Ok(cart);
		}
	}
}
