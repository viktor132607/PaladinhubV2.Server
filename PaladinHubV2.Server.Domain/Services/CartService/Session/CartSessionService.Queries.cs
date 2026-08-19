using Microsoft.EntityFrameworkCore;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed partial class CartSessionService
	{
		public async Task<int> GetCount(
			string userId,
			CancellationToken cancellationToken)
		{
			if (!TryNormalizeOwner(userId, out string ownerKey))
			{
				return 0;
			}

			if (IsAnonymousOwner(ownerKey))
			{
				var lines = await _cartStore.GetAsync(ownerKey, cancellationToken);
				return lines.Sum(line => Math.Max(0, line.Quantity));
			}

			int? count = await _db.Carts
				.AsNoTracking()
				.Where(cart => cart.UserId == ownerKey && !cart.IsArchived)
				.SelectMany(cart => cart.CartProducts)
				.Select(cartProduct => (int?)cartProduct.Quantity)
				.SumAsync(cancellationToken);

			return count ?? 0;
		}
	}
}
