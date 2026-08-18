using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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
	public sealed class CartsController : ControllerBase
	{
		private readonly ICartService _cartService;
		private readonly IProductService _productService;
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;

		public CartsController(
			ICartService cartService,
			IProductService productService,
			UserManager<User> userManager,
			ICartSessionService cartSession)
		{
			_cartService = cartService;
			_productService = productService;
			_userManager = userManager;
			_cartSession = cartSession;
		}

		[HttpGet("my-cart")]
		[HttpGet("MyCart")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> MyCart(
			CancellationToken cancellationToken)
		{
			var user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var model = await GetCartViewModelAsync(
				user,
				cancellationToken);

			return Ok(model);
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
				return CartError(
					"Product ID is required.");
			}

			var owner = OwnerKey();

			var updated = await _cartSession.IncreaseProduct(
				id.Trim(),
				owner,
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be increased.");
			}

			return await CartDeltaAsync(
				id.Trim(),
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
				return CartError(
					"Product ID is required.");
			}

			var owner = OwnerKey();

			var updated = await _cartSession.DecreaseProduct(
				id.Trim(),
				owner,
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be decreased.");
			}

			return await CartDeltaAsync(
				id.Trim(),
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
				return CartError(
					"Product ID is required.");
			}

			var owner = OwnerKey();

			var removed = await _cartSession.RemoveProduct(
				id.Trim(),
				owner,
				cancellationToken);

			if (!removed)
			{
				return CartError(
					"The product could not be removed.");
			}

			return await CartDeltaAsync(
				id.Trim(),
				removed: true,
				cancellationToken);
		}

		[HttpPost("Cancel")]
		public async Task<IActionResult> Cancel(
			CancellationToken cancellationToken)
		{
			var user = await CurrentUserAsync();

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

		[AllowAnonymous]
		[HttpGet("Mini")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Mini()
		{
			var user = await CurrentUserAsync();

			if (user == null)
			{
				return Ok(new MyCartViewModel
				{
					TotalPrice = 0m
				});
			}

			var model =
				await _productService.GetMyProducts(user);

			return Ok(model);
		}

		[AllowAnonymous]
		[HttpGet("CountJson")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> CountJson(
			CancellationToken cancellationToken)
		{
			var count = await _cartSession.GetCount(
				OwnerKey(),
				cancellationToken);

			return Ok(count);
		}

		[Authorize(Roles = "Admin")]
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

		[Authorize(Roles = "Admin")]
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

		private async Task<IActionResult> AddProductCore(
			string? productId,
			int quantity,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(productId))
			{
				return CartError(
					"Product ID is required.");
			}

			if (quantity < 1 || quantity > 100)
			{
				return CartError(
					"Quantity must be between 1 and 100.");
			}

			var normalizedProductId =
				productId.Trim();

			var owner = OwnerKey();

			for (var index = 0; index < quantity; index++)
			{
				var added = await _cartSession.AddProduct(
					normalizedProductId,
					owner,
					cancellationToken);

				if (!added)
				{
					return CartError(
						"The product could not be added to the cart.");
				}
			}

			var cartCount = await _cartSession.GetCount(
				owner,
				cancellationToken);

			return Ok(new
			{
				ok = true,
				productId = normalizedProductId,
				quantityAdded = quantity,
				cartCount,
				message = "Product added to the cart."
			});
		}

		private async Task<IActionResult> CartDeltaAsync(
			string productId,
			bool? removed,
			CancellationToken cancellationToken)
		{
			var user = await CurrentUserAsync();

			if (user == null)
			{
				var cartCount = await _cartSession.GetCount(
					OwnerKey(),
					cancellationToken);

				return Ok(new
				{
					ok = true,
					productId,
					removed = removed ?? false,
					cartCount
				});
			}

			var model = await GetCartViewModelAsync(
				user,
				cancellationToken);

			var item = model.MyProducts.FirstOrDefault(
				product => product.Id == productId);

			var isRemoved =
				removed ??
				item == null;

			var quantity =
				item?.Quantity ??
				0;

			var unitPrice =
				item?.Price ??
				0m;

			var lineTotal =
				item == null
					? 0m
					: item.Price * item.Quantity;

			return Ok(new
			{
				ok = true,
				productId,
				removed = isRemoved,
				quantity,
				unitPrice,
				lineTotal = isRemoved
					? 0m
					: lineTotal,
				cartTotal = model.TotalPrice
			});
		}

		private async Task<MyCartViewModel> GetCartViewModelAsync(
			User user,
			CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			return await _productService.GetMyProducts(user);
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

		public sealed class AddCartItemRequest
		{
			public string ProductId { get; init; } =
				string.Empty;

			public int Quantity { get; init; } = 1;
		}
	}
}
