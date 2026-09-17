using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
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
		private readonly IProductService _productService;
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;
		private readonly ICartStore? _cartStore;
		private readonly AppDbContext? _db;
		private readonly CartFlowService _cartFlow;

		public CartsController(
			IProductService productService,
			UserManager<User> userManager,
			ICartSessionService cartSession,
			ICartStore? cartStore = null,
			AppDbContext? db = null,
			CartFlowService? cartFlow = null)
		{
			_productService = productService;
			_userManager = userManager;
			_cartSession = cartSession;
			_cartStore = cartStore;
			_db = db;
			_cartFlow = cartFlow ?? new CartFlowService(cartSession, productService);
		}

		[AllowAnonymous]
		[HttpGet("my-cart")]
		[HttpGet("MyCart")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> MyCart(
			CancellationToken cancellationToken = default)
		{
			User? user = await CurrentCheckoutUserAsync();

			MyCartViewModel model =
				user == null
					? await GetAnonymousCartAsync(cancellationToken)
					: await _cartFlow.GetCartViewModelAsync(
						user,
						cancellationToken);

			return Ok(model);
		}

		[AllowAnonymous]
		[HttpGet("Mini")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Mini(
			CancellationToken cancellationToken = default)
		{
			User? user = await CurrentCheckoutUserAsync();

			MyCartViewModel model =
				user == null
					? await GetAnonymousCartAsync(cancellationToken)
					: await _productService.GetMyProducts(user);

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
			int count = await _cartSession.GetCount(
				OwnerKey(),
				cancellationToken);

			return Ok(count);
		}

		private Task<User?> CurrentCheckoutUserAsync()
		{
			return CheckoutGuestUserResolver.ResolveExistingAsync(
				HttpContext,
				User,
				_userManager);
		}

		private string OwnerKey()
		{
			return CheckoutGuestUserResolver.GetOwnerKey(
				HttpContext,
				User);
		}

		private async Task<MyCartViewModel> GetAnonymousCartAsync(
			CancellationToken cancellationToken)
		{
			var model = new MyCartViewModel();

			if (_cartStore == null || _db == null)
			{
				return model;
			}

			var lines = await _cartStore.GetAsync(
				OwnerKey(),
				cancellationToken);

			if (lines.Count == 0)
			{
				return model;
			}

			string[] productIds = lines
				.Select(line => line.ProductId.ToString())
				.ToArray();

			var products = await _db.Products
				.AsNoTracking()
				.Include(product => product.ThumbnailImage)
				.Include(product => product.Images)
				.Where(product => productIds.Contains(product.Id))
				.ToListAsync(cancellationToken);

			var productsById = products.ToDictionary(
				product => product.Id,
				StringComparer.OrdinalIgnoreCase);

			foreach (var line in lines)
			{
				string productId = line.ProductId.ToString();

				if (!productsById.TryGetValue(
						productId,
						out Product? product))
				{
					continue;
				}

				string imageUrl =
					product.ThumbnailImage?.Url ??
					product.Images
						.OrderBy(image => image.SortOrder)
						.Select(image => image.Url)
						.FirstOrDefault() ??
					string.Empty;

				model.MyProducts.Add(new ProductViewModel
				{
					Id = product.Id,
					Name = product.Name,
					Price = product.Price,
					ImageUrl = imageUrl,
					Quantity = line.Quantity,
					CartId = Guid.Empty,
					Cart = null!,
					Category = product.Category,
					Description = product.Description
				});

				model.TotalPrice += product.Price * line.Quantity;
			}

			return model;
		}
	}
}
