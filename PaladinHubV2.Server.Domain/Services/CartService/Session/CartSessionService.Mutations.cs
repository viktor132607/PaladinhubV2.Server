using Microsoft.EntityFrameworkCore;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed partial class CartSessionService
	{
		public async Task<bool> AddProduct(
			string productId,
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey) ||
				!TryNormalizeProductId(productId, out Guid productGuid, out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.AddProduct(normalizedProductId, ownerKey);
			}

			bool productExists = await _db.Products
				.AsNoTracking()
				.AnyAsync(product => product.Id == normalizedProductId, cancellationToken);

			if (!productExists)
			{
				return false;
			}

			var lines = await _cartStore.GetAsync(ownerKey, cancellationToken);
			var existing = lines.FirstOrDefault(line => line.ProductId == productGuid);

			int newQuantity = existing == null ? 1 : existing.Quantity + 1;

			await _cartStore.AddOrUpdateAsync(
				ownerKey,
				productGuid,
				newQuantity,
				cancellationToken);

			return true;
		}

		public async Task<bool> IncreaseProduct(
			string productId,
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey) ||
				!TryNormalizeProductId(productId, out Guid productGuid, out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.IncreaseProduct(normalizedProductId, ownerKey);
			}

			var lines = await _cartStore.GetAsync(ownerKey, cancellationToken);
			var existing = lines.FirstOrDefault(line => line.ProductId == productGuid);

			if (existing == null || existing.Quantity <= 0)
			{
				return false;
			}

			await _cartStore.AddOrUpdateAsync(
				ownerKey,
				productGuid,
				existing.Quantity + 1,
				cancellationToken);

			return true;
		}

		public async Task<bool> DecreaseProduct(
			string productId,
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey) ||
				!TryNormalizeProductId(productId, out Guid productGuid, out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.DecreaseProduct(normalizedProductId, ownerKey);
			}

			var lines = await _cartStore.GetAsync(ownerKey, cancellationToken);
			var existing = lines.FirstOrDefault(line => line.ProductId == productGuid);

			if (existing == null || existing.Quantity <= 0)
			{
				return false;
			}

			int newQuantity = Math.Max(0, existing.Quantity - 1);

			await _cartStore.AddOrUpdateAsync(
				ownerKey,
				productGuid,
				newQuantity,
				cancellationToken);

			return true;
		}

		public async Task<bool> RemoveProduct(
			string productId,
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey) ||
				!TryNormalizeProductId(productId, out Guid productGuid, out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.RemoveProduct(normalizedProductId, ownerKey);
			}

			var lines = await _cartStore.GetAsync(ownerKey, cancellationToken);
			bool productExists = lines.Any(line => line.ProductId == productGuid);

			if (!productExists)
			{
				return false;
			}

			await _cartStore.AddOrUpdateAsync(
				ownerKey,
				productGuid,
				0,
				cancellationToken);

			return true;
		}
	}
}
