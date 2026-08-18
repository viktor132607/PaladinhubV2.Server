using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using PaladinHub.Common;
using PaladinHub.Models.Carts;

namespace PaladinHubV2.Server.Domain.Services
{
	public sealed class MemoryCartStore : ICartStore
	{
		private static readonly ConcurrentDictionary<
			string,
			SemaphoreSlim> KeyLocks = new();

		private static readonly JsonSerializerOptions JsonOptions =
			new(JsonSerializerDefaults.Web);

		private readonly IDistributedCache _cache;

		public MemoryCartStore(IDistributedCache cache)
		{
			_cache = cache;
		}

		public async Task AddOrUpdateAsync(
			string userId,
			Guid productId,
			int quantity,
			CancellationToken cancellationToken)
		{
			string ownerKey = NormalizeUserId(userId);

			if (productId == Guid.Empty)
			{
				throw new ArgumentException(
					"Product ID cannot be empty.",
					nameof(productId));
			}

			string cacheKey = BuildKey(ownerKey);
			SemaphoreSlim keyLock = GetKeyLock(cacheKey);

			await keyLock.WaitAsync(cancellationToken);

			try
			{
				List<CartLine> cart = await ReadCartAsync(
					cacheKey,
					cancellationToken);

				cart.RemoveAll(line =>
					line.ProductId == productId);

				if (quantity > 0)
				{
					cart.Add(new CartLine
					{
						ProductId = productId,
						Quantity = quantity
					});
				}

				if (cart.Count == 0)
				{
					await _cache.RemoveAsync(
						cacheKey,
						cancellationToken);

					return;
				}

				string payload = JsonSerializer.Serialize(
					cart,
					JsonOptions);

				await _cache.SetStringAsync(
					cacheKey,
					payload,
					CreateCacheOptions(),
					cancellationToken);
			}
			finally
			{
				keyLock.Release();
			}
		}

		public async Task<IReadOnlyList<CartLine>> GetAsync(
			string userId,
			CancellationToken cancellationToken)
		{
			string ownerKey = NormalizeUserId(userId);
			string cacheKey = BuildKey(ownerKey);
			SemaphoreSlim keyLock = GetKeyLock(cacheKey);

			await keyLock.WaitAsync(cancellationToken);

			try
			{
				List<CartLine> cart = await ReadCartAsync(
					cacheKey,
					cancellationToken);

				return cart;
			}
			finally
			{
				keyLock.Release();
			}
		}

		public async Task ClearAsync(
			string userId,
			CancellationToken cancellationToken)
		{
			string ownerKey = NormalizeUserId(userId);
			string cacheKey = BuildKey(ownerKey);
			SemaphoreSlim keyLock = GetKeyLock(cacheKey);

			await keyLock.WaitAsync(cancellationToken);

			try
			{
				await _cache.RemoveAsync(
					cacheKey,
					cancellationToken);
			}
			finally
			{
				keyLock.Release();
			}
		}

		private async Task<List<CartLine>> ReadCartAsync(
			string cacheKey,
			CancellationToken cancellationToken)
		{
			string? payload = await _cache.GetStringAsync(
				cacheKey,
				cancellationToken);

			if (string.IsNullOrWhiteSpace(payload))
			{
				return new List<CartLine>();
			}

			try
			{
				List<CartLine>? deserialized =
					JsonSerializer.Deserialize<List<CartLine>>(
						payload,
						JsonOptions);

				if (deserialized == null)
				{
					return new List<CartLine>();
				}

				return deserialized
					.Where(line =>
						line.ProductId != Guid.Empty &&
						line.Quantity > 0)
					.GroupBy(line => line.ProductId)
					.Select(group => new CartLine
					{
						ProductId = group.Key,
						Quantity = group.Sum(line =>
							line.Quantity)
					})
					.ToList();
			}
			catch (JsonException)
			{
				await _cache.RemoveAsync(
					cacheKey,
					cancellationToken);

				return new List<CartLine>();
			}
		}

		private static string NormalizeUserId(string? userId)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				throw new ArgumentException(
					"Cart owner ID is required.",
					nameof(userId));
			}

			return userId.Trim();
		}

		private static string BuildKey(string userId)
		{
			return Constants.Cart.RedisPrefix + userId;
		}

		private static SemaphoreSlim GetKeyLock(
			string cacheKey)
		{
			return KeyLocks.GetOrAdd(
				cacheKey,
				static _ => new SemaphoreSlim(1, 1));
		}

		private static DistributedCacheEntryOptions
			CreateCacheOptions()
		{
			return new DistributedCacheEntryOptions
			{
				SlidingExpiration = TimeSpan.FromHours(
					Constants.Cart.TtlHours)
			};
		}
	}
}
