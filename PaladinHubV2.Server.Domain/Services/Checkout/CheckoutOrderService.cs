using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public readonly record struct CheckoutCartSnapshot(
		int Items,
		decimal Total);

	public sealed record CheckoutPaymentReview(
		decimal? WalletBalance,
		string? PaymentError);

	public sealed record CheckoutOrderPlacementResult(
		bool Success,
		string? ErrorMessage = null);

	public interface ICheckoutOrderService
	{
		Task<CheckoutCartSnapshot> GetCartSnapshotAsync(
			User user,
			CancellationToken cancellationToken);

		Task<CheckoutPaymentReview> GetPaymentReviewAsync(
			User user,
			CheckoutState state,
			decimal total);

		Task<bool> OrderTransactionExistsAsync(
			string userId,
			string orderId,
			CancellationToken cancellationToken);

		Task<CheckoutOrderPlacementResult> PlaceCashOnDeliveryAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken);

		Task<CheckoutOrderPlacementResult> PlaceWalletAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken);

		Task CompleteCardOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken);

		Task ArchiveCartAsync(
			User user,
			CancellationToken cancellationToken);
	}

	public sealed class CheckoutOrderService : ICheckoutOrderService
	{
		private const string Currency = "USD";
		private const string Region = "US";

		private readonly ICartSessionService _cartSession;
		private readonly IProductService _productService;
		private readonly IWalletService _wallet;
		private readonly AppDbContext _db;

		public CheckoutOrderService(
			ICartSessionService cartSession,
			IProductService productService,
			IWalletService wallet,
			AppDbContext db)
		{
			_cartSession = cartSession;
			_productService = productService;
			_wallet = wallet;
			_db = db;
		}

		public async Task<CheckoutCartSnapshot> GetCartSnapshotAsync(
			User user,
			CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			var cart =
				await _productService.GetMyProducts(user);

			return new CheckoutCartSnapshot(
				cart.MyProducts?.Count ?? 0,
				cart.TotalPrice);
		}

		public async Task<CheckoutPaymentReview> GetPaymentReviewAsync(
			User user,
			CheckoutState state,
			decimal total)
		{
			decimal? walletBalance = null;
			string? paymentError = null;

			if (state.PaymentMethod ==
				PaladinHub.Models.Checkout.PaymentMethod.Balance)
			{
				walletBalance =
					await _wallet.GetBalanceAsync(user.Id);

				if (walletBalance < total)
				{
					paymentError =
						"Insufficient wallet balance.";
				}
			}

			return new CheckoutPaymentReview(
				walletBalance,
				paymentError);
		}

		public async Task<bool> OrderTransactionExistsAsync(
			string userId,
			string orderId,
			CancellationToken cancellationToken)
		{
			return await _db.Transactions
				.AsNoTracking()
				.AnyAsync(
					transaction =>
						transaction.UserId == userId &&
						transaction.ExternalId == orderId,
					cancellationToken);
		}

		public async Task<CheckoutOrderPlacementResult>
			PlaceCashOnDeliveryAsync(
				User user,
				CheckoutState state,
				string orderId,
				CancellationToken cancellationToken)
		{
			bool alreadyProcessed =
				await OrderTransactionExistsAsync(
					user.Id,
					orderId,
					cancellationToken);

			if (!alreadyProcessed)
			{
				await LogPurchaseTransactionAsync(
					user,
					state,
					TransactionStatus.Pending,
					cancellationToken);
			}

			await ArchiveCartAsync(
				user,
				cancellationToken);

			return new CheckoutOrderPlacementResult(true);
		}

		public async Task<CheckoutOrderPlacementResult>
			PlaceWalletAsync(
				User user,
				CheckoutState state,
				string orderId,
				CancellationToken cancellationToken)
		{
			bool alreadyProcessed =
				await OrderTransactionExistsAsync(
					user.Id,
					orderId,
					cancellationToken);

			if (!alreadyProcessed)
			{
				try
				{
					Guid transactionId =
						await _wallet.ChargeAsync(
							user.Id,
							state.Total,
							$"Order {orderId} (Wallet)");

					await AttachOrderMetadataAsync(
						transactionId,
						orderId,
						cancellationToken);
				}
				catch (InvalidOperationException)
				{
					return new CheckoutOrderPlacementResult(
						false,
						"Insufficient wallet balance.");
				}
			}

			await ArchiveCartAsync(
				user,
				cancellationToken);

			return new CheckoutOrderPlacementResult(true);
		}

		public async Task CompleteCardOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken)
		{
			await LogPurchaseTransactionAsync(
				user,
				state,
				TransactionStatus.Complete,
				cancellationToken);

			await ArchiveCartAsync(
				user,
				cancellationToken);
		}

		public Task ArchiveCartAsync(
			User user,
			CancellationToken cancellationToken)
		{
			return _cartSession.ArchiveAndClear(
				user,
				cancellationToken);
		}

		private async Task LogPurchaseTransactionAsync(
			User user,
			CheckoutState state,
			TransactionStatus status,
			CancellationToken cancellationToken)
		{
			if (state.Total <= 0m ||
				string.IsNullOrWhiteSpace(state.OrderId))
			{
				return;
			}

			bool alreadyExists =
				await OrderTransactionExistsAsync(
					user.Id,
					state.OrderId,
					cancellationToken);

			if (alreadyExists)
			{
				return;
			}

			var transaction =
				new Transaction
				{
					Id = Guid.NewGuid(),
					UserId = user.Id,
					CreatedAtUtc = DateTime.UtcNow,

					PurchaseTitle =
						$"Order {state.OrderId} " +
						$"({state.PaymentMethod})",

					Amount = state.Total,
					Currency = Currency,
					Region = Region,
					Status = status,
					ExternalId = state.OrderId,
					Type = TransactionType.Purchase
				};

			_db.Transactions.Add(transaction);

			await _db.SaveChangesAsync(
				cancellationToken);
		}

		private async Task AttachOrderMetadataAsync(
			Guid transactionId,
			string orderId,
			CancellationToken cancellationToken)
		{
			Transaction? transaction =
				await _db.Transactions.FirstOrDefaultAsync(
					item => item.Id == transactionId,
					cancellationToken);

			if (transaction == null)
			{
				throw new InvalidOperationException(
					"Wallet transaction was not found.");
			}

			transaction.ExternalId = orderId;
			transaction.Region = Region;

			await _db.SaveChangesAsync(
				cancellationToken);
		}
	}
}
