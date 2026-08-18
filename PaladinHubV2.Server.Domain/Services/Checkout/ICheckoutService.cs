using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public enum CheckoutResultCode
	{
		Success,
		MissingShipping,
		MissingCheckoutDetails,
		EmptyCart,
		InvalidPaymentMethod,
		InsufficientWallet,
		CardNotSelected,
		MissingOrderId,
		StripeNotConfigured,
		StripeCreateFailed,
		StripeVerificationFailed,
		PaymentNotCompleted,
		PaymentCurrencyMismatch,
		PaymentOrderMismatch,
		PaymentUserMismatch,
		CartTotalChanged
	}

	public sealed record CheckoutOperationResult<T>(
		bool Succeeded,
		CheckoutResultCode Code,
		T? Value = default,
		string? Message = null,
		string? Redirect = null)
	{
		public static CheckoutOperationResult<T> Ok(T value) =>
			new(true, CheckoutResultCode.Success, value);

		public static CheckoutOperationResult<T> Fail(
			CheckoutResultCode code,
			string message,
			string? redirect = null) =>
			new(false, code, default, message, redirect);
	}

	public sealed record CheckoutReviewData(
		ShippingInfoVM Shipping,
		CheckoutPaymentMethod PaymentMethod,
		decimal Total,
		int Items,
		decimal? WalletBalance,
		string? PaymentError,
		string? OrderId);

	public sealed record CheckoutPlacementData(
		string OrderId,
		string Redirect,
		bool ClearState);

	public sealed record CheckoutCardSessionData(
		string ClientSecret,
		string PublishableKey,
		string PaymentIntentId,
		string OrderId,
		decimal Amount,
		string Currency);

	public sealed record CheckoutFinalizeData(
		string OrderId,
		string Redirect,
		bool ClearState);

	public interface ICheckoutService
	{
		Task<CheckoutOperationResult<CheckoutReviewData>> ReviewAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken);

		Task<CheckoutOperationResult<CheckoutPlacementData>> PlaceOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken);

		Task<CheckoutOperationResult<CheckoutCardSessionData>> CreateCardSessionAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken);

		Task<CheckoutOperationResult<CheckoutFinalizeData>> FinalizeCardAsync(
			User user,
			CheckoutState state,
			string paymentIntentId,
			CancellationToken cancellationToken);
	}
}
