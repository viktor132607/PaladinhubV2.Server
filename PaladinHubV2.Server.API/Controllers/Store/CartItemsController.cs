using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartItemsController : CartControllerBase
	{
		private readonly ICartSessionService _cartSession;

		public CartItemsController(
			UserManager<User> userManager,
			ICartSessionService cartSession,
			CartFlowService cartFlow)
			: base(userManager, cartFlow)
		{
			_cartSession = cartSession;
		}

		[AllowAnonymous]
		[HttpPost("items")]
		public Task<IActionResult> AddItem(
			[FromBody] AddCartItemRequest request,
			CancellationToken cancellationToken)
		{
			if (request == null)
			{
				return Task.FromResult<IActionResult>(
					BadRequest(new
					{
						ok = false,
						message = "Cart item data is required."
					}));
			}

			return AddProductCore(
				request.ProductId,
				request.Quantity,
				cancellationToken);
		}

		[AllowAnonymous]
		[HttpGet("AddProduct/{id}")]
		[HttpGet("~/Carts/AddProduct/{id}")]
		public Task<IActionResult> AddProduct(
			[FromRoute] string id,
			CancellationToken cancellationToken)
		{
			return AddProductCore(id, 1, cancellationToken);
		}

		[AllowAnonymous]
		[HttpPost("RemoveProduct")]
		public async Task<IActionResult> RemoveProduct(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return CartError("Product ID is required.");
			}

			string productId = id.Trim();
			bool removed = await _cartSession.RemoveProduct(
				productId,
				OwnerKey(),
				cancellationToken);

			if (!removed)
			{
				return CartError("The product could not be removed.");
			}

			return await CartDeltaAsync(
				productId,
				removed: true,
				cancellationToken);
		}

		private async Task<IActionResult> AddProductCore(
			string? productId,
			int quantity,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(productId))
			{
				return CartError("Product ID is required.");
			}

			if (quantity < 1 || quantity > 100)
			{
				return CartError("Quantity must be between 1 and 100.");
			}

			CartAddResult result = await CartFlow.AddProductAsync(
				productId,
				quantity,
				OwnerKey(),
				cancellationToken);

			if (!result.Succeeded)
			{
				return CartError("The product could not be added to the cart.");
			}

			return Ok(new
			{
				ok = true,
				productId = result.ProductId,
				quantityAdded = result.QuantityAdded,
				cartCount = result.CartCount,
				message = "Product added to the cart."
			});
		}
	}
}
