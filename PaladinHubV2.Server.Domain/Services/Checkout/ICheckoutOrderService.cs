using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public interface ICheckoutOrderService
	{
		Task<bool> OrderExistsAsync(
			string userId,
			string orderId,
			CancellationToken cancellationToken);

		Task PlaceCashOnDeliveryAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken);

		Task<bool> PlaceWalletAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken);

		Task CompleteCardOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken);

		Task ArchiveProcessedOrderAsync(
			User user,
			CancellationToken cancellationToken);
	}
}
