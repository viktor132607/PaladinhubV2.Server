using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public sealed class CheckoutService : ICheckoutService
	{
		private readonly ICartSessionService _cartSession;
		private readonly IProductService _productService;
		private readonly IWalletService _wallet;
		private readonly ICheckoutOrderService _orders;
		private readonly ICheckoutPaymentService _payments;

		public CheckoutService(
			ICartSessionService cartSession,
			IProductService productService,
			IWalletService wallet,
			ICheckoutOrderService orders,
			ICheckoutPaymentService payments)
		{
			_cartSession = cartSession;
			_productService = productService;
			_wallet = wallet;
			_orders = orders;
			_payments = payments;
		}

		public async Task<CheckoutOperationResult<CheckoutReviewData>> ReviewAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken)
		{
			if (state.Shipping == null || state.PaymentMethod == null)
			{
				return CheckoutOperationResult<CheckoutReviewData>.Fail(
					CheckoutResultCode.MissingCheckoutDetails,
					"Shipping details or payment method are missing.",
					"/Checkout/Shipping");
			}

			CheckoutCartSnapshot snapshot = await GetCartSnapshot(
				user,
				cancellationToken);

			state.Total = snapshot.Total;

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return CheckoutOperationResult<CheckoutReviewData>.Fail(
					CheckoutResultCode.EmptyCart,
					"Your cart is empty.",
					"/Cart/MyCart");
			}

			decimal? walletBalance = null;
			string? paymentError = null;

			if (state.PaymentMethod == CheckoutPaymentMethod.Balance)
			{
				walletBalance = await _wallet.GetBalanceAsync(user.Id);

				if (walletBalance < state.Total)
				{
					paymentError = "Insufficient wallet balance.";
				}
			}

			return CheckoutOperationResult<CheckoutReviewData>.Ok(
				new CheckoutReviewData(
					state.Shipping,
					state.PaymentMethod.Value,
					state.Total,
					snapshot.Items,
					walletBalance,
					paymentError,
					state.OrderId));
		}

		public async Task<CheckoutOperationResult<CheckoutPlacementData>> PlaceOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken)
		{
			if (state.Shipping == null || state.PaymentMethod == null)
			{
				return CheckoutOperationResult<CheckoutPlacementData>.Fail(
					CheckoutResultCode.MissingCheckoutDetails,
					"Shipping details or payment method are missing.",
					"/Checkout/Shipping");
			}

			CheckoutCartSnapshot snapshot = await GetCartSnapshot(
				user,
				cancellationToken);

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return CheckoutOperationResult<CheckoutPlacementData>.Fail(
					CheckoutResultCode.EmptyCart,
					"Your cart is empty.",
					"/Cart/MyCart");
			}

			state.Total = snapshot.Total;
			state.OrderId ??= Guid.NewGuid().ToString("N");

			string orderId = state.OrderId;

			switch (state.PaymentMethod.Value)
			{
				case CheckoutPaymentMethod.CashOnDelivery:
					await _orders.PlaceCashOnDeliveryAsync(
						user,
						state,
						orderId,
						cancellationToken);

					return CheckoutOperationResult<CheckoutPlacementData>.Ok(
						new CheckoutPlacementData(
							orderId,
							"/Checkout/Registered",
							true));

				case CheckoutPaymentMethod.Balance:
					bool walletCharged = await _orders.PlaceWalletAsync(
						user,
						state,
						orderId,
						cancellationToken);

					if (!walletCharged)
					{
						return CheckoutOperationResult<CheckoutPlacementData>.Fail(
							CheckoutResultCode.InsufficientWallet,
							"Insufficient wallet balance.",
							"/Checkout/Review");
					}

					return CheckoutOperationResult<CheckoutPlacementData>.Ok(
						new CheckoutPlacementData(
							orderId,
							"/Checkout/Success",
							true));

				case CheckoutPaymentMethod.Card:
					return CheckoutOperationResult<CheckoutPlacementData>.Ok(
						new CheckoutPlacementData(
							orderId,
							"/Checkout/Card",
							false));

				default:
					return CheckoutOperationResult<CheckoutPlacementData>.Fail(
						CheckoutResultCode.InvalidPaymentMethod,
						"Invalid payment method.");
			}
		}

		public async Task<CheckoutOperationResult<CheckoutCardSessionData>> CreateCardSessionAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken)
		{
			if (state.Shipping == null)
			{
				return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
					CheckoutResultCode.MissingShipping,
					"Shipping details are required.",
					"/Checkout/Shipping");
			}

			if (state.PaymentMethod != CheckoutPaymentMethod.Card)
			{
				return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
					CheckoutResultCode.CardNotSelected,
					"Card payment is not selected.",
					"/Checkout/Review");
			}

			CheckoutCartSnapshot snapshot = await GetCartSnapshot(
				user,
				cancellationToken);

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return CheckoutOperationResult<CheckoutCardSessionData>.Fail(
					CheckoutResultCode.EmptyCart,
					"Your cart is empty.",
					"/Cart/MyCart");
			}

			state.Total = snapshot.Total;
			state.OrderId ??= Guid.NewGuid().ToString("N");

			return await _payments.CreateCardSessionAsync(
				user.Id,
				state.OrderId,
				state.Total,
				cancellationToken);
		}

		public async Task<CheckoutOperationResult<CheckoutFinalizeData>> FinalizeCardAsync(
			User user,
			CheckoutState state,
			string paymentIntentId,
			CancellationToken cancellationToken)
		{
			if (state.PaymentMethod != CheckoutPaymentMethod.Card)
			{
				return CheckoutOperationResult<CheckoutFinalizeData>.Fail(
					CheckoutResultCode.CardNotSelected,
					"Card payment is not selected.");
			}

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				return CheckoutOperationResult<CheckoutFinalizeData>.Fail(
					CheckoutResultCode.MissingOrderId,
					"Checkout order ID is missing.");
			}

			string orderId = state.OrderId;

			if (await _orders.OrderExistsAsync(
					user.Id,
					orderId,
					cancellationToken))
			{
				await _orders.ArchiveProcessedOrderAsync(
					user,
					cancellationToken);

				return CheckoutOperationResult<CheckoutFinalizeData>.Ok(
					new CheckoutFinalizeData(
						orderId,
						BuildSuccessRedirect(orderId),
						true));
			}

			CheckoutCartSnapshot snapshot = await GetCartSnapshot(
				user,
				cancellationToken);

			if (snapshot.Items <= 0 || snapshot.Total <= 0m)
			{
				return CheckoutOperationResult<CheckoutFinalizeData>.Fail(
					CheckoutResultCode.EmptyCart,
					"Your cart is empty.");
			}

			CheckoutOperationResult<bool> verification =
				await _payments.VerifyCardPaymentAsync(
					user.Id,
					orderId,
					snapshot.Total,
					paymentIntentId,
					cancellationToken);

			if (!verification.Succeeded)
			{
				return CheckoutOperationResult<CheckoutFinalizeData>.Fail(
					verification.Code,
					verification.Message ?? "Card payment verification failed.",
					verification.Redirect);
			}

			state.Total = snapshot.Total;

			await _orders.CompleteCardOrderAsync(
				user,
				state,
				cancellationToken);

			return CheckoutOperationResult<CheckoutFinalizeData>.Ok(
				new CheckoutFinalizeData(
					orderId,
					BuildSuccessRedirect(orderId),
					true));
		}

		private async Task<CheckoutCartSnapshot> GetCartSnapshot(
			User user,
			CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			var cart = await _productService.GetMyProducts(user);

			return new CheckoutCartSnapshot(
				cart.MyProducts?.Count ?? 0,
				cart.TotalPrice);
		}

		private static string BuildSuccessRedirect(string orderId)
		{
			return $"/Checkout/Success?orderId={Uri.EscapeDataString(orderId)}";
		}

		private sealed record CheckoutCartSnapshot(
			int Items,
			decimal Total);
	}
}
