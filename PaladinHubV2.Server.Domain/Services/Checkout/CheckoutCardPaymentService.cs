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
		string Currency = "EUR");

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
		Task<CheckoutCardSessionResult> CreateSessionAsync(string userId, string orderId, decimal total, string currency, CancellationToken cancellationToken);

		Task<CheckoutCardVerificationResult> VerifyAsync(
			string paymentIntentId,
			string userId,
			string orderId,
			CancellationToken cancellationToken);
		Task<CheckoutCardVerificationResult> VerifyAsync(string paymentIntentId, string userId, string orderId, string currency, CancellationToken cancellationToken);

		bool AmountMatches(
			decimal total,
			long paidAmount);
	}

	public sealed class CheckoutCardPaymentService :
		ICheckoutCardPaymentService
	{
		private const string Currency = "EUR";
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
			=> await CreateSessionAsync(userId, orderId, total, Currency, cancellationToken);

		public async Task<CheckoutCardSessionResult> CreateSessionAsync(
			string userId, string orderId, decimal total, string currency, CancellationToken cancellationToken)
		{
			if (currency is not ("EUR" or "USD")) return new CheckoutCardSessionResult(CheckoutCardPaymentError.CreateFailed);
			long amountInCents =
				ToMinorUnits(total);

			var options =
				new PaymentIntentCreateOptions
				{
					Amount = amountInCents,
					Currency = currency.ToLowerInvariant(),

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
					currency);
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
			=> await VerifyAsync(paymentIntentId, userId, orderId, Currency, cancellationToken);

		public async Task<CheckoutCardVerificationResult> VerifyAsync(
			string paymentIntentId, string userId, string orderId, string currency, CancellationToken cancellationToken)
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
					currency,
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
