using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public enum CheckoutCardFlowError
	{
		None,
		ShippingRequired,
		CardNotSelected,
		EmptyCart,
		StripeNotConfigured,
		MissingClientSecret,
		SessionCreateFailed,
		MissingOrderId,
		PaymentVerificationFailed,
		AmountChanged
	}

	public sealed record CheckoutCardSetupResult(
		CheckoutCardFlowError Error,
		string? ClientSecret = null,
		string? PublishableKey = null,
		string? PaymentIntentId = null,
		string? OrderId = null,
		decimal Amount = 0m,
		string? Currency = null);

	public sealed record CheckoutCardFinalizeResult(
		CheckoutCardFlowError Error,
		string? OrderId = null,
		CheckoutCardPaymentError PaymentError =
			CheckoutCardPaymentError.None);

	public sealed class CheckoutCardFlowService
	{
		private readonly ICheckoutSessionService _checkoutSession;
		private readonly ICheckoutOrderService _checkoutOrders;
		private readonly ICheckoutCardPaymentService _cardPayments;

		public CheckoutCardFlowService(
			ICheckoutSessionService checkoutSession,
			ICheckoutOrderService checkoutOrders,
			ICheckoutCardPaymentService cardPayments)
		{
			_checkoutSession = checkoutSession;
			_checkoutOrders = checkoutOrders;
			_cardPayments = cardPayments;
		}

		public async Task<CheckoutCardSetupResult> PrepareAsync(
			User user,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			CheckoutState state = _checkoutSession.GetState();

			if (state.Shipping == null)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.ShippingRequired);
			}

			if (state.PaymentMethod != CheckoutPaymentMethod.Card)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.CardNotSelected);
			}

			CheckoutCartSnapshot snapshot =
				await _checkoutOrders.GetCartSnapshotAsync(
					user,
					cancellationToken);

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.EmptyCart);
			}

			if (!_cardPayments.IsConfigured)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.StripeNotConfigured);
			}

			state.Total = snapshot.Total;

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				state.OrderId = Guid.NewGuid().ToString("N");
			}

			_checkoutSession.SaveState(state);

			CheckoutCardSessionResult paymentSession =
				await _cardPayments.CreateSessionAsync(
					user.Id,
					state.OrderId,
					state.Total,
					cancellationToken);

			if (paymentSession.Error ==
				CheckoutCardPaymentError.MissingClientSecret)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.MissingClientSecret);
			}

			if (paymentSession.Error != CheckoutCardPaymentError.None)
			{
				return new CheckoutCardSetupResult(
					CheckoutCardFlowError.SessionCreateFailed);
			}

			return new CheckoutCardSetupResult(
				CheckoutCardFlowError.None,
				paymentSession.ClientSecret,
				paymentSession.PublishableKey,
				paymentSession.PaymentIntentId,
				state.OrderId,
				state.Total,
				paymentSession.Currency);
		}

		public async Task<CheckoutCardFinalizeResult> FinalizeAsync(
			User user,
			string paymentIntentId,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(user);

			CheckoutState state = _checkoutSession.GetState();

			if (state.PaymentMethod != CheckoutPaymentMethod.Card)
			{
				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.CardNotSelected);
			}

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.MissingOrderId);
			}

			string orderId = state.OrderId;

			if (await _checkoutOrders.OrderTransactionExistsAsync(
					user.Id,
					orderId,
					cancellationToken))
			{
				await _checkoutOrders.ArchiveCartAsync(
					user,
					cancellationToken);

				_checkoutSession.Clear();

				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.None,
					orderId);
			}

			CheckoutCardVerificationResult verification =
				await _cardPayments.VerifyAsync(
					paymentIntentId,
					user.Id,
					orderId,
					cancellationToken);

			if (verification.Error != CheckoutCardPaymentError.None)
			{
				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.PaymentVerificationFailed,
					orderId,
					verification.Error);
			}

			CheckoutCartSnapshot snapshot =
				await _checkoutOrders.GetCartSnapshotAsync(
					user,
					cancellationToken);

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.EmptyCart,
					orderId);
			}

			if (!_cardPayments.AmountMatches(
					snapshot.Total,
					verification.Amount))
			{
				return new CheckoutCardFinalizeResult(
					CheckoutCardFlowError.AmountChanged,
					orderId);
			}

			state.Total = snapshot.Total;
			_checkoutSession.SaveState(state);

			await _checkoutOrders.CompleteCardOrderAsync(
				user,
				state,
				cancellationToken);

			_checkoutSession.Clear();

			return new CheckoutCardFinalizeResult(
				CheckoutCardFlowError.None,
				orderId);
		}
	}
}
