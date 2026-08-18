//using System.Text.Json;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using PaladinHub.Common;
//using PaladinHub.Models.Carts;
//using StackExchange.Redis;

//namespace PaladinHub.Services
//{
//	public sealed class RedisCartStore : ICartStore
//	{
//		private readonly IDatabase db;

//		public RedisCartStore(IConnectionMultiplexer connection)
//		{
//			db = connection.GetDatabase();
//		}

//		public async Task AddOrUpdateAsync(string userId, Guid productId, int quantity, CancellationToken ct)
//		{
//			var key = Constants.Cart.RedisPrefix + userId;
//			var value = await db.StringGetAsync(key);

//			var cart = string.IsNullOrEmpty(value)
//				? new List<CartLine>()
//				: (JsonSerializer.Deserialize<List<CartLine>>(value!) ?? new List<CartLine>());

//			var existing = cart.FirstOrDefault(x => x.ProductId == productId);
//			if (existing is null)
//				cart.Add(new CartLine { ProductId = productId, Quantity = quantity });
//			else
//				existing.Quantity = quantity;

//			var serialized = JsonSerializer.Serialize(cart);
//			await db.StringSetAsync(key, serialized, TimeSpan.FromHours(Constants.Cart.TtlHours));
//		}

//		public async Task<IReadOnlyList<CartLine>> GetAsync(string userId, CancellationToken ct)
//		{
//			var key = Constants.Cart.RedisPrefix + userId;
//			var value = await db.StringGetAsync(key);
//			if (string.IsNullOrEmpty(value))
//				return new List<CartLine>();

//			await db.KeyExpireAsync(key, TimeSpan.FromHours(Constants.Cart.TtlHours));
//			return JsonSerializer.Deserialize<List<CartLine>>(value!) ?? new List<CartLine>();
//		}

//		public async Task ClearAsync(string userId, CancellationToken ct)
//		{
//			var key = Constants.Cart.RedisPrefix + userId;
//			await db.KeyDeleteAsync(key);
//		}
//	}
//}
