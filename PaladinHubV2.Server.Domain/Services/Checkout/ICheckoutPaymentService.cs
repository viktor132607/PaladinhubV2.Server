namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public interface ICheckoutPaymentService
	{
		Task<CheckoutOperationResult<CheckoutCardSessionData>> CreateCardSessionAsync(
			string userId,
			string orderId,
			decimal total,
			CancellationToken cancellationToken);

		Task<CheckoutOperationResult<bool>> VerifyCardPaymentAsync(
			string userId,
			string orderId,
			decimal expectedTotal,
			string paymentIntentId,
			CancellationToken cancellationToken);
	}
}
