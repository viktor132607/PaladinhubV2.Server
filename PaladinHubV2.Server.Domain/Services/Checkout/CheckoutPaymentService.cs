using Microsoft.Extensions.Configuration;
using Stripe;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public sealed class CheckoutPaymentService : ICheckoutPaymentService
	{
		private const string Currency = "USD";
		private const string StripeCurrency = "usd";

		private readonly string _stripePublishableKey;

		public CheckoutPaymentService(IConfiguration configuration)
		{
			_stripePublishableKey =
				configuration["Stripe:PublishableKey"] ?? string.Empty;
		}

		public async Task<CheckoutOperationResult<CheckoutCardSessionData>> CreateCardSessionAsync(
			string userId,
			string orderId,
			decimal total,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(_stripePublishableKey) ||
				string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
			{
				return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
					CheckoutResultCode.StripeNotConfigured,
					"Stripe is not configured.");
			}

			var options = new PaymentIntentCreateOptions
			{
				Amount = ToMinorUnits(total),
				Currency = StripeCurrency,
				Description = $"PaladinHub order {orderId}",
				PaymentMethodTypes = new List<string> { "card" },
				Metadata = new Dictionary<string, string>
				{
					["orderId"] = orderId,
					["userId"] = userId
				}
			};

			try
			{
				var stripeService = new PaymentIntentService();
				PaymentIntent intent = await stripeService.CreateAsync(
					options,
					null,
					cancellationToken);

				if (string.IsNullOrWhiteSpace(intent.ClientSecret))
				{
					return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
						CheckoutResultCode.StripeCreateFailed,
						"Stripe did not return a client secret.");
				}

				return CheckoutOperationResult<CheckoutCardSessionData>.Ok(
					new CheckoutCardSessionData(
						intent.ClientSecret,
						_stripePublishableKey,
						intent.Id,
						orderId,
						total,
						Currency));
			}
			catch (StripeException)
			{
				return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
					CheckoutResultCode.StripeCreateFailed,
					"Card payment session could not be created.");
			}
		}

		public async Task<CheckoutOperationResult<bool>> VerifyCardPaymentAsync(
			string userId,
			string orderId,
			decimal expectedTotal,
			string paymentIntentId,
			CancellationToken cancellationToken)
		{
			PaymentIntent paymentIntent;

			try
			{
				var stripeService = new PaymentIntentService();
				paymentIntent = await stripeService.GetAsync(
					paymentIntentId.Trim(),
					null,
					null,
					cancellationToken);
			}
			catch (StripeException)
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.StripeVerificationFailed,
					"Stripe payment could not be verified.");
			}

			if (!string.Equals(
					paymentIntent.Status,
					"succeeded",
					StringComparison.OrdinalIgnoreCase))
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.PaymentNotCompleted,
					"Payment was not completed.");
			}

			if (!string.Equals(
					paymentIntent.Currency,
					StripeCurrency,
					StringComparison.OrdinalIgnoreCase))
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.PaymentCurrencyMismatch,
					"Payment currency does not match the order.");
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"orderId",
					out string? stripeOrderId) ||
				!string.Equals(stripeOrderId, orderId, StringComparison.Ordinal))
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.PaymentOrderMismatch,
					"Payment order does not match the checkout order.");
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"userId",
					out string? stripeUserId) ||
				!string.Equals(stripeUserId, userId, StringComparison.Ordinal))
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.PaymentUserMismatch,
					"Payment user does not match the checkout user.");
			}

			if (paymentIntent.Amount != ToMinorUnits(expectedTotal))
			{
				return CheckoutOperationResult<bool>.Fail(
					CheckoutResultCode.CartTotalChanged,
					"The cart total changed after the payment session was created.");
			}

			return CheckoutOperationResult<bool>.Ok(true);
		}

		private static long ToMinorUnits(decimal amount)
		{
			return checked(
				(long)decimal.Round(
					amount * 100m,
					0,
					MidpointRounding.AwayFromZero));
		}
	}
}
