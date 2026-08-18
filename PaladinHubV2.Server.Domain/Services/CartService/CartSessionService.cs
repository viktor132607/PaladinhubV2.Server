using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed class CartSessionService : ICartSessionService
	{
		private const string AnonymousOwnerPrefix = "anon:";

		private readonly ICartService _cartService;
		private readonly ICartStore _cartStore;
		private readonly AppDbContext _db;

		public CartSessionService(
			ICartService cartService,
			ICartStore cartStore,
			AppDbContext db)
		{
			_cartService = cartService;
			_cartStore = cartStore;
			_db = db;
		}

		public async Task<bool> AddProduct(
			string productId,
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey) ||
				!TryNormalizeProductId(
					productId,
					out Guid productGuid,
					out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.AddProduct(
					normalizedProductId,
					ownerKey);
			}

			bool productExists = await _db.Products
				.AsNoTracking()
				.AnyAsync(
					product =>
						product.Id == normalizedProductId,
					cancellationToken);

			if (!productExists)
			{
				return false;
			}

			var lines = await _cartStore.GetAsync(
				ownerKey,
				cancellationToken);

			var existing = lines.FirstOrDefault(
				line => line.ProductId == productGuid);

			int newQuantity =
				existing == null
					? 1
					: existing.Quantity + 1;

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
				!TryNormalizeProductId(
					productId,
					out Guid productGuid,
					out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.IncreaseProduct(
					normalizedProductId,
					ownerKey);
			}

			var lines = await _cartStore.GetAsync(
				ownerKey,
				cancellationToken);

			var existing = lines.FirstOrDefault(
				line => line.ProductId == productGuid);

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
				!TryNormalizeProductId(
					productId,
					out Guid productGuid,
					out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.DecreaseProduct(
					normalizedProductId,
					ownerKey);
			}

			var lines = await _cartStore.GetAsync(
				ownerKey,
				cancellationToken);

			var existing = lines.FirstOrDefault(
				line => line.ProductId == productGuid);

			if (existing == null || existing.Quantity <= 0)
			{
				return false;
			}

			int newQuantity =
				Math.Max(0, existing.Quantity - 1);

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
				!TryNormalizeProductId(
					productId,
					out Guid productGuid,
					out string normalizedProductId))
			{
				return false;
			}

			if (!IsAnonymousOwner(ownerKey))
			{
				return await _cartService.RemoveProduct(
					normalizedProductId,
					ownerKey);
			}

			var lines = await _cartStore.GetAsync(
				ownerKey,
				cancellationToken);

			bool productExists = lines.Any(
				line => line.ProductId == productGuid);

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

		public async Task ArchiveAndClear(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			await _cartService.ArchiveCart(user);

			await _cartStore.ClearAsync(
				user.Id,
				cancellationToken);
		}

		public async Task CleanAndClear(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			await _cartService.CleanCart(user);

			await _cartStore.ClearAsync(
				user.Id,
				cancellationToken);
		}

		public async Task SyncRedisToPersistent(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			var lines = await _cartStore.GetAsync(
				user.Id,
				cancellationToken);

			if (lines.Count == 0)
			{
				return;
			}

			await _cartService.CleanCart(user);

			foreach (var line in lines)
			{
				if (line.Quantity <= 0)
				{
					continue;
				}

				string productId =
					line.ProductId.ToString();

				for (int index = 0;
					 index < line.Quantity;
					 index++)
				{
					cancellationToken
						.ThrowIfCancellationRequested();

					await _cartService.AddProduct(
						productId,
						user.Id);
				}
			}

			await _cartStore.ClearAsync(
				user.Id,
				cancellationToken);
		}

		public async Task<int> GetCount(
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(
					userId,
					out string ownerKey))
			{
				return 0;
			}

			if (IsAnonymousOwner(ownerKey))
			{
				var lines = await _cartStore.GetAsync(
					ownerKey,
					cancellationToken);

				return lines.Sum(
					line => Math.Max(0, line.Quantity));
			}

			int? count = await _db.Carts
				.AsNoTracking()
				.Where(cart =>
					cart.UserId == ownerKey &&
					!cart.IsArchived)
				.SelectMany(cart => cart.CartProducts)
				.Select(cartProduct =>
					(int?)cartProduct.Quantity)
				.SumAsync(cancellationToken);

			return count ?? 0;
		}

		private static bool TryNormalizeOwner(
			string? userId,
			out string ownerKey)
		{
			ownerKey = userId?.Trim() ??
				string.Empty;

			return !string.IsNullOrWhiteSpace(ownerKey);
		}

		private static bool TryNormalizeProductId(
			string? productId,
			out Guid productGuid,
			out string normalizedProductId)
		{
			normalizedProductId = string.Empty;

			if (!Guid.TryParse(
					productId?.Trim(),
					out productGuid))
			{
				return false;
			}

			normalizedProductId =
				productGuid.ToString();

			return true;
		}

		private static bool IsAnonymousOwner(
			string ownerKey)
		{
			return ownerKey.StartsWith(
				AnonymousOwnerPrefix,
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
