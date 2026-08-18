using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed class CartApplicationService : ICartApplicationService
	{
		private readonly ICartSessionService _cartSession;
		private readonly IProductService _productService;

		public CartApplicationService(
			ICartSessionService cartSession,
			IProductService productService)
		{
			_cartSession = cartSession;
			_productService = productService;
		}

		public async Task<MyCartViewModel> GetCartAsync(
			User user,
			CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			return await _productService.GetMyProducts(user);
		}

		public async Task<MyCartViewModel> GetMiniCartAsync(
			User? user,
			CancellationToken cancellationToken)
		{
			if (user == null)
			{
				return new MyCartViewModel
				{
					TotalPrice = 0m
				};
			}

			return await _productService.GetMyProducts(user);
		}

		public Task<int> GetCountAsync(
			string ownerKey,
			CancellationToken cancellationToken)
		{
			return _cartSession.GetCount(
				ownerKey,
				cancellationToken);
		}

		public async Task<CartAddResult> AddAsync(
			string? productId,
			int quantity,
			string ownerKey,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeProductId(
					productId,
					out string normalizedProductId))
			{
				return AddFailure("Product ID is required.");
			}

			if (quantity is < 1 or > 100)
			{
				return AddFailure(
					"Quantity must be between 1 and 100.");
			}

			for (int index = 0; index < quantity; index++)
			{
				bool added = await _cartSession.AddProduct(
					normalizedProductId,
					ownerKey,
					cancellationToken);

				if (!added)
				{
					return AddFailure(
						"The product could not be added to the cart.");
				}
			}

			int cartCount = await _cartSession.GetCount(
				ownerKey,
				cancellationToken);

			return new CartAddResult
			{
				Succeeded = true,
				ProductId = normalizedProductId,
				QuantityAdded = quantity,
				CartCount = cartCount
			};
		}

		public async Task<CartDeltaResult> IncreaseAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeProductId(
					productId,
					out string normalizedProductId))
			{
				return DeltaFailure("Product ID is required.");
			}

			bool updated = await _cartSession.IncreaseProduct(
				normalizedProductId,
				ownerKey,
				cancellationToken);

			if (!updated)
			{
				return DeltaFailure(
					"The product quantity could not be increased.");
			}

			return await BuildDeltaAsync(
				normalizedProductId,
				removed: false,
				ownerKey,
				user,
				cancellationToken);
		}

		public async Task<CartDeltaResult> DecreaseAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeProductId(
					productId,
					out string normalizedProductId))
			{
				return DeltaFailure("Product ID is required.");
			}

			bool updated = await _cartSession.DecreaseProduct(
				normalizedProductId,
				ownerKey,
				cancellationToken);

			if (!updated)
			{
				return DeltaFailure(
					"The product quantity could not be decreased.");
			}

			return await BuildDeltaAsync(
				normalizedProductId,
				removed: null,
				ownerKey,
				user,
				cancellationToken);
		}

		public async Task<CartDeltaResult> RemoveAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeProductId(
					productId,
					out string normalizedProductId))
			{
				return DeltaFailure("Product ID is required.");
			}

			bool removed = await _cartSession.RemoveProduct(
				normalizedProductId,
				ownerKey,
				cancellationToken);

			if (!removed)
			{
				return DeltaFailure(
					"The product could not be removed.");
			}

			return await BuildDeltaAsync(
				normalizedProductId,
				removed: true,
				ownerKey,
				user,
				cancellationToken);
		}

		public Task ClearAsync(
			User user,
			CancellationToken cancellationToken)
		{
			return _cartSession.CleanAndClear(
				user,
				cancellationToken);
		}

		private async Task<CartDeltaResult> BuildDeltaAsync(
			string productId,
			bool? removed,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken)
		{
			if (user == null)
			{
				int cartCount = await _cartSession.GetCount(
					ownerKey,
					cancellationToken);

				return new CartDeltaResult
				{
					Succeeded = true,
					ProductId = productId,
					Removed = removed ?? false,
					CartCount = cartCount,
					HasDetailedTotals = false
				};
			}

			MyCartViewModel model = await GetCartAsync(
				user,
				cancellationToken);

			var item = model.MyProducts.FirstOrDefault(
				product => product.Id == productId);

			bool isRemoved = removed ?? item == null;
			int quantity = item?.Quantity ?? 0;
			decimal unitPrice = item?.Price ?? 0m;
			decimal lineTotal =
				item == null
					? 0m
					: item.Price * item.Quantity;

			return new CartDeltaResult
			{
				Succeeded = true,
				ProductId = productId,
				Removed = isRemoved,
				HasDetailedTotals = true,
				Quantity = quantity,
				UnitPrice = unitPrice,
				LineTotal = isRemoved ? 0m : lineTotal,
				CartTotal = model.TotalPrice
			};
		}

		private static bool TryNormalizeProductId(
			string? productId,
			out string normalizedProductId)
		{
			normalizedProductId =
				productId?.Trim() ??
				string.Empty;

			return !string.IsNullOrWhiteSpace(normalizedProductId);
		}

		private static CartAddResult AddFailure(string error)
		{
			return new CartAddResult
			{
				Succeeded = false,
				Error = error
			};
		}

		private static CartDeltaResult DeltaFailure(string error)
		{
			return new CartDeltaResult
			{
				Succeeded = false,
				Error = error
			};
		}
	}
}
