using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed partial class CartSessionService
	{
		public async Task ArchiveAndClear(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			await _cartService.ArchiveCart(user);
			await _cartStore.ClearAsync(user.Id, cancellationToken);
		}

		public async Task CleanAndClear(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			await _cartService.CleanCart(user);
			await _cartStore.ClearAsync(user.Id, cancellationToken);
		}

		public async Task SyncRedisToPersistent(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			var lines = await _cartStore.GetAsync(user.Id, cancellationToken);
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

				string productId = line.ProductId.ToString();

				for (int index = 0; index < line.Quantity; index++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await _cartService.AddProduct(productId, user.Id);
				}
			}

			await _cartStore.ClearAsync(user.Id, cancellationToken);
		}
	}
}
