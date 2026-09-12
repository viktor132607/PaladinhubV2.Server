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

		public CartArchiveController(
			ICartService cartService)
		{
			_cartService = cartService;
		}

		[HttpGet("archive")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Archive()
		{
			var archivedCarts =
				await _cartService.GetArchive();

			var response = archivedCarts.Select(cart => new
			{
				id = cart.Id,
				username =
					cart.User?.UserName ??
					"Unknown",
				orderDate =
					cart.OrderDate ??
					string.Empty
			});

			return Ok(response);
		}

		[HttpGet("archive/{id:guid}")]
		[HttpGet("Details/{id:guid}")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Details(
			[FromRoute] Guid id)
		{
			if (id == Guid.Empty)
			{
				return BadRequest(new
				{
					message = "Invalid cart ID."
				});
			}

			var cart =
				await _cartService.GetCartById(id);

			if (cart == null)
			{
				return NotFound(new
				{
					message = "Archived cart not found."
				});
			}

			return Ok(cart);
		}
	}
}
