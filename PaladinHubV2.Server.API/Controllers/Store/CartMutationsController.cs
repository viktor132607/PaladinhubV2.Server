using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartMutationsController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;
		private readonly CartFlowService _cartFlow;

		public CartMutationsController(
			IProductService productService,
			UserManager<User> userManager,
			ICartSessionService cartSession)
		{
			_userManager = userManager;
			_cartSession = cartSession;
			_cartFlow = new CartFlowService(
				cartSession,
				productService);
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
			if (string.IsNullOrWhiteSpace(id))
			{
				return CartError("Product ID is required.");
			}

			string productId = id.Trim();
			string owner = OwnerKey();

			bool updated = await _cartSession.IncreaseProduct(
				productId,
				owner,
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be increased.");
			}

			return await CartDeltaAsync(
				productId,
				removed: false,
				cancellationToken);
		}

		[AllowAnonymous]
		[HttpPost("Decrease")]
		public async Task<IActionResult> Decrease(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return CartError("Product ID is required.");
			}

			string productId = id.Trim();
			string owner = OwnerKey();

			bool updated = await _cartSession.DecreaseProduct(
				productId,
				owner,
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be decreased.");
			}

			return await CartDeltaAsync(
				productId,
				removed: null,
				cancellationToken);
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
			string owner = OwnerKey();

			bool removed = await _cartSession.RemoveProduct(
				productId,
				owner,
				cancellationToken);

			if (!removed)
			{
				return CartError(
					"The product could not be removed.");
			}

			return await CartDeltaAsync(
				productId,
				removed: true,
				cancellationToken);
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

			await _cartSession.CleanAndClear(
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
			if (string.IsNullOrWhiteSpace(productId))
			{
				return CartError("Product ID is required.");
			}

			if (quantity < 1 || quantity > 100)
			{
				return CartError(
					"Quantity must be between 1 and 100.");
			}

			CartAddResult result = await _cartFlow.AddProductAsync(
				productId,
				quantity,
				OwnerKey(),
				cancellationToken);

			if (!result.Succeeded)
			{
				return CartError(
					"The product could not be added to the cart.");
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

		private async Task<IActionResult> CartDeltaAsync(
			string productId,
			bool? removed,
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			CartDeltaResult delta = await _cartFlow.GetDeltaAsync(
				user,
				OwnerKey(),
				productId,
				removed,
				cancellationToken);

			if (delta.IsAnonymous)
			{
				return Ok(new
				{
					ok = true,
					productId = delta.ProductId,
					removed = delta.Removed,
					cartCount = delta.CartCount
				});
			}

			return Ok(new
			{
				ok = true,
				productId = delta.ProductId,
				removed = delta.Removed,
				quantity = delta.Quantity,
				unitPrice = delta.UnitPrice,
				lineTotal = delta.LineTotal,
				cartTotal = delta.CartTotal
			});
		}

		private string? CurrentUserId()
		{
			return User.FindFirstValue(
				ClaimTypes.NameIdentifier);
		}

		private Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		private string OwnerKey()
		{
			return CurrentUserId() ??
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
	}
}
