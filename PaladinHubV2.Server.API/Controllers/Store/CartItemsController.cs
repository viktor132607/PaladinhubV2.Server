using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartItemsController : ControllerBase
	{
		private readonly ICartApplicationService _cart;
		private readonly UserManager<User> _userManager;

		public CartItemsController(
			ICartApplicationService cart,
			UserManager<User> userManager)
		{
			_cart = cart;
			_userManager = userManager;
		}

		[AllowAnonymous]
		[HttpPost("items")]
		public Task<IActionResult> AddItem(
			[FromBody] AddCartItemRequest? request,
			CancellationToken cancellationToken)
		{
			if (request == null)
			{
				return Task.FromResult<IActionResult>(
					CartError("Cart item data is required."));
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
			return AddProductCore(
				id,
				1,
				cancellationToken);
		}

		[AllowAnonymous]
		[HttpPost("Increase")]
		public async Task<IActionResult> Increase(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();
			CartDeltaResult result = await _cart.IncreaseAsync(
				id,
				OwnerKey(),
				user,
				cancellationToken);

			return DeltaResponse(result);
		}

		[AllowAnonymous]
		[HttpPost("Decrease")]
		public async Task<IActionResult> Decrease(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();
			CartDeltaResult result = await _cart.DecreaseAsync(
				id,
				OwnerKey(),
				user,
				cancellationToken);

			return DeltaResponse(result);
		}

		[AllowAnonymous]
		[HttpPost("RemoveProduct")]
		public async Task<IActionResult> RemoveProduct(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();
			CartDeltaResult result = await _cart.RemoveAsync(
				id,
				OwnerKey(),
				user,
				cancellationToken);

			return DeltaResponse(result);
		}

		[HttpPost("Cancel")]
		public async Task<IActionResult> Cancel(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					ok = false,
					message = "Authentication required."
				});
			}

			await _cart.ClearAsync(
				user,
				cancellationToken);

			return Ok(new
			{
				ok = true,
				cleared = true,
				cartTotal = 0m,
				message = "Cart was cleared."
			});
		}

		private async Task<IActionResult> AddProductCore(
			string? productId,
			int quantity,
			CancellationToken cancellationToken)
		{
			CartAddResult result = await _cart.AddAsync(
				productId,
				quantity,
				OwnerKey(),
				cancellationToken);

			if (!result.Succeeded)
			{
				return CartError(
					result.Error ?? "Cart update failed.");
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

		private IActionResult DeltaResponse(CartDeltaResult result)
		{
			if (!result.Succeeded)
			{
				return CartError(
					result.Error ?? "Cart update failed.");
			}

			if (!result.HasDetailedTotals)
			{
				return Ok(new
				{
					ok = true,
					productId = result.ProductId,
					removed = result.Removed,
					cartCount = result.CartCount
				});
			}

			return Ok(new
			{
				ok = true,
				productId = result.ProductId,
				removed = result.Removed,
				quantity = result.Quantity,
				unitPrice = result.UnitPrice,
				lineTotal = result.LineTotal,
				cartTotal = result.CartTotal
			});
		}

		private Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		private string OwnerKey()
		{
			return User.FindFirstValue(
					ClaimTypes.NameIdentifier) ??
				$"anon:{HttpContext.Session.Id}";
		}

		private IActionResult CartError(string message)
		{
			return BadRequest(new
			{
				ok = false,
				message
			});
		}

		public sealed class AddCartItemRequest
		{
			public string ProductId { get; init; } = string.Empty;
			public int Quantity { get; init; } = 1;
		}
	}
}
