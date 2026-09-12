using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed record CartAddResult(
		bool Succeeded,
		string ProductId,
		int QuantityAdded,
		int CartCount);

	public sealed record CartDeltaResult(
		bool IsAnonymous,
		string ProductId,
		bool Removed,
		int CartCount,
		int Quantity,
		decimal UnitPrice,
		decimal LineTotal,
		decimal CartTotal);

	public sealed class CartFlowService
	{
		private readonly ICartSessionService _cartSession;
		private readonly IProductService _productService;

		public CartFlowService(
			ICartSessionService cartSession,
			IProductService productService)
		{
			_cartSession = cartSession;
			_productService = productService;
		}

		public async Task<CartAddResult> AddProductAsync(
			string productId,
			int quantity,
			string ownerKey,
			CancellationToken cancellationToken)
		{
			string normalizedProductId = productId.Trim();

			for (int index = 0; index < quantity; index++)
			{
				bool added = await _cartSession.AddProduct(
					normalizedProductId,
					ownerKey,
					cancellationToken);

				if (!added)
				{
					return new CartAddResult(
						false,
						normalizedProductId,
						0,
						0);
				}
			}

			int cartCount = await _cartSession.GetCount(
				ownerKey,
				cancellationToken);

			return new CartAddResult(
				true,
				normalizedProductId,
				quantity,
				cartCount);
		}

		public async Task<MyCartViewModel> GetCartViewModelAsync(
			User user,
			CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			return await _productService.GetMyProducts(user);
		}

		public async Task<CartDeltaResult> GetDeltaAsync(
			User? user,
			string ownerKey,
			string productId,
			bool? removed,
			CancellationToken cancellationToken)
		{
			if (user == null)
			{
				int cartCount = await _cartSession.GetCount(
					ownerKey,
					cancellationToken);

				return new CartDeltaResult(
					true,
					productId,
					removed ?? false,
					cartCount,
					0,
					0m,
					0m,
					0m);
			}

			MyCartViewModel model = await GetCartViewModelAsync(
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

			return new CartDeltaResult(
				false,
				productId,
				isRemoved,
				0,
				quantity,
				unitPrice,
				isRemoved ? 0m : lineTotal,
				model.TotalPrice);
		}
	}
}
