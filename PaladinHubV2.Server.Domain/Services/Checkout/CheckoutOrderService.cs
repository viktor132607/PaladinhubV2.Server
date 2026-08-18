using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public sealed class CheckoutOrderService : ICheckoutOrderService
	{
		private const string Currency = "USD";
		private const string Region = "US";

		private readonly AppDbContext _db;
		private readonly IWalletService _wallet;
		private readonly ICartSessionService _cartSession;

		public CheckoutOrderService(
			AppDbContext db,
			IWalletService wallet,
			ICartSessionService cartSession)
		{
			_db = db;
			_wallet = wallet;
			_cartSession = cartSession;
		}

		public Task<bool> OrderExistsAsync(
			string userId,
			string orderId,
			CancellationToken cancellationToken)
		{
			return _db.Transactions
				.AsNoTracking()
				.AnyAsync(
					transaction =>
						transaction.UserId == userId &&
						transaction.ExternalId == orderId,
					cancellationToken);
		}

		public async Task PlaceCashOnDeliveryAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken)
		{
			if (!await OrderExistsAsync(user.Id, orderId, cancellationToken))
			{
				await LogPurchaseTransaction(
					user,
					state,
					TransactionStatus.Pending,
					cancellationToken);
			}

			await _cartSession.ArchiveAndClear(user, cancellationToken);
		}

		public async Task<bool> PlaceWalletAsync(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken)
		{
			if (!await OrderExistsAsync(user.Id, orderId, cancellationToken))
			{
				try
				{
					Guid transactionId = await _wallet.ChargeAsync(
						user.Id,
						state.Total,
						$"Order {orderId} (Wallet)");

					await AttachOrderMetadata(
						transactionId,
						orderId,
						cancellationToken);
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}

			await _cartSession.ArchiveAndClear(user, cancellationToken);
			return true;
		}

		public async Task CompleteCardOrderAsync(
			User user,
			CheckoutState state,
			CancellationToken cancellationToken)
		{
			await LogPurchaseTransaction(
				user,
				state,
				TransactionStatus.Complete,
				cancellationToken);

			await _cartSession.ArchiveAndClear(user, cancellationToken);
		}

		public Task ArchiveProcessedOrderAsync(
			User user,
			CancellationToken cancellationToken)
		{
			return _cartSession.ArchiveAndClear(user, cancellationToken);
		}

		private async Task LogPurchaseTransaction(
			User user,
			CheckoutState state,
			TransactionStatus status,
			CancellationToken cancellationToken)
		{
			if (state.Total <= 0m || string.IsNullOrWhiteSpace(state.OrderId))
			{
				return;
			}

			if (await OrderExistsAsync(
					user.Id,
					state.OrderId,
					cancellationToken))
			{
				return;
			}

			var transaction = new Transaction
			{
				Id = Guid.NewGuid(),
				UserId = user.Id,
				CreatedAtUtc = DateTime.UtcNow,
				PurchaseTitle = $"Order {state.OrderId} ({state.PaymentMethod})",
				Amount = state.Total,
				Currency = Currency,
				Region = Region,
				Status = status,
				ExternalId = state.OrderId,
				Type = TransactionType.Purchase
			};

			_db.Transactions.Add(transaction);
			await _db.SaveChangesAsync(cancellationToken);
		}

		private async Task AttachOrderMetadata(
			Guid transactionId,
			string orderId,
			CancellationToken cancellationToken)
		{
			Transaction? transaction = await _db.Transactions.FirstOrDefaultAsync(
				item => item.Id == transactionId,
				cancellationToken);

			if (transaction == null)
			{
				throw new InvalidOperationException(
					"Wallet transaction was not found.");
			}

			transaction.ExternalId = orderId;
			transaction.Region = Region;

			await _db.SaveChangesAsync(cancellationToken);
		}
	}
}
