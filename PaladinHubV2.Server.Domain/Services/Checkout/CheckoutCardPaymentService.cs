using Microsoft.Extensions.Configuration;
using Stripe;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public enum CheckoutCardPaymentError
	{
		None = 0,
		MissingClientSecret = 1,
		CreateFailed = 2,
		VerificationFailed = 3,
		PaymentNotCompleted = 4,
		CurrencyMismatch = 5,
		OrderMismatch = 6,
		UserMismatch = 7
	}

	public sealed record CheckoutCardSessionResult(
		CheckoutCardPaymentError Error,
		string? ClientSecret = null,
		string? PublishableKey = null,
		string? PaymentIntentId = null,
		decimal Amount = 0m,
		string Currency = "USD");

	public sealed record CheckoutCardVerificationResult(
		CheckoutCardPaymentError Error,
		long Amount = 0L);

	public interface ICheckoutCardPaymentService
	{
		bool IsConfigured { get; }

		Task<CheckoutCardSessionResult> CreateSessionAsync(
			string userId,
			string orderId,
			decimal total,
			CancellationToken cancellationToken);

		Task<CheckoutCardVerificationResult> VerifyAsync(
			string paymentIntentId,
			string userId,
			string orderId,
			CancellationToken cancellationToken);

		bool AmountMatches(
			decimal total,
			long paidAmount);
	}

	public sealed class CheckoutCardPaymentService :
		ICheckoutCardPaymentService
	{
		private const string Currency = "USD";
		private readonly string _stripePublishableKey;

		public CheckoutCardPaymentService(
			IConfiguration configuration)
		{
			_stripePublishableKey =
				configuration["Stripe:PublishableKey"] ??
				string.Empty;
		}

		public bool IsConfigured =>
			!string.IsNullOrWhiteSpace(
				_stripePublishableKey) &&
			!string.IsNullOrWhiteSpace(
				StripeConfiguration.ApiKey);

		public async Task<CheckoutCardSessionResult>
			CreateSessionAsync(
				string userId,
				string orderId,
				decimal total,
				CancellationToken cancellationToken)
		{
			long amountInCents =
				ToMinorUnits(total);

			var options =
				new PaymentIntentCreateOptions
				{
					Amount = amountInCents,
					Currency = "usd",

					Description =
						$"PaladinHub order {orderId}",

					PaymentMethodTypes =
						new List<string>
						{
							"card"
						},

					Metadata =
						new Dictionary<string, string>
						{
							["orderId"] = orderId,
							["userId"] = userId
						}
				};

			try
			{
				var service =
					new PaymentIntentService();

				PaymentIntent intent =
					await service.CreateAsync(
						options,
						null,
						cancellationToken);

				if (string.IsNullOrWhiteSpace(
						intent.ClientSecret))
				{
					return new CheckoutCardSessionResult(
						CheckoutCardPaymentError
							.MissingClientSecret);
				}

				return new CheckoutCardSessionResult(
					CheckoutCardPaymentError.None,
					intent.ClientSecret,
					_stripePublishableKey,
					intent.Id,
					total,
					Currency);
			}
			catch (StripeException)
			{
				return new CheckoutCardSessionResult(
					CheckoutCardPaymentError.CreateFailed);
			}
		}

		public async Task<CheckoutCardVerificationResult>
			VerifyAsync(
				string paymentIntentId,
				string userId,
				string orderId,
				CancellationToken cancellationToken)
		{
			PaymentIntent paymentIntent;

			try
			{
				var service =
					new PaymentIntentService();

				paymentIntent =
					await service.GetAsync(
						paymentIntentId.Trim(),
						null,
						null,
						cancellationToken);
			}
			catch (StripeException)
			{
				return new CheckoutCardVerificationResult(
					CheckoutCardPaymentError
						.VerificationFailed);
			}

			if (!string.Equals(
					paymentIntent.Status,
					"succeeded",
					StringComparison.OrdinalIgnoreCase))
			{
				return new CheckoutCardVerificationResult(
					CheckoutCardPaymentError
						.PaymentNotCompleted);
			}

			if (!string.Equals(
					paymentIntent.Currency,
					"usd",
					StringComparison.OrdinalIgnoreCase))
			{
				return new CheckoutCardVerificationResult(
					CheckoutCardPaymentError
						.CurrencyMismatch);
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"orderId",
					out string? stripeOrderId) ||
				!string.Equals(
					stripeOrderId,
					orderId,
					StringComparison.Ordinal))
			{
				return new CheckoutCardVerificationResult(
					CheckoutCardPaymentError
						.OrderMismatch);
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"userId",
					out string? stripeUserId) ||
				!string.Equals(
					stripeUserId,
					userId,
					StringComparison.Ordinal))
			{
				return new CheckoutCardVerificationResult(
					CheckoutCardPaymentError
						.UserMismatch);
			}

			return new CheckoutCardVerificationResult(
				CheckoutCardPaymentError.None,
				paymentIntent.Amount);
		}

		public bool AmountMatches(
			decimal total,
			long paidAmount)
		{
			return ToMinorUnits(total) == paidAmount;
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
