using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Wallet
{
	public sealed class WalletService : IWalletService
	{
		private const string WalletCurrency = "USD";
		private const int MaximumTitleLength = 160;

		private readonly AppDbContext _db;

		public WalletService(AppDbContext db)
		{
			_db = db;
		}

		public async Task<decimal> GetBalanceAsync(string userId)
		{
			ValidateUserId(userId);

			return await GetBalanceQuery(userId)
				.SumAsync(transaction =>
					(decimal?)transaction.Amount) ?? 0m;
		}

		public async Task<Guid> TopUpAsync(
			string userId,
			decimal amount,
			string title = "Balance Top-up")
		{
			ValidateUserId(userId);

			decimal normalizedAmount =
				ValidateAmount(amount);

			string normalizedTitle =
				ValidateTitle(title);

			var transaction = new Transaction
			{
				Id = Guid.NewGuid(),
				UserId = userId.Trim(),
				PurchaseTitle = normalizedTitle,
				Amount = normalizedAmount,
				Currency = WalletCurrency,
				CreatedAtUtc = DateTime.UtcNow,
				Status = TransactionStatus.Complete,
				Type = TransactionType.WalletTopUp
			};

			_db.Transactions.Add(transaction);

			await _db.SaveChangesAsync();

			return transaction.Id;
		}

		public async Task<Guid> ChargeAsync(
			string userId,
			decimal amount,
			string title)
		{
			ValidateUserId(userId);

			decimal normalizedAmount =
				ValidateAmount(amount);

			string normalizedTitle =
				ValidateTitle(title);

			await using var databaseTransaction =
				await _db.Database.BeginTransactionAsync(
					IsolationLevel.Serializable);

			decimal currentBalance =
				await GetBalanceQuery(userId)
					.SumAsync(transaction =>
						(decimal?)transaction.Amount) ?? 0m;

			if (currentBalance < normalizedAmount)
			{
				throw new InvalidOperationException(
					"Insufficient wallet balance.");
			}

			var transaction = new Transaction
			{
				Id = Guid.NewGuid(),
				UserId = userId.Trim(),
				PurchaseTitle = normalizedTitle,
				Amount = -normalizedAmount,
				Currency = WalletCurrency,
				CreatedAtUtc = DateTime.UtcNow,
				Status = TransactionStatus.Complete,
				Type = TransactionType.WalletCharge
			};

			_db.Transactions.Add(transaction);

			await _db.SaveChangesAsync();
			await databaseTransaction.CommitAsync();

			return transaction.Id;
		}

		private IQueryable<Transaction> GetBalanceQuery(
			string userId)
		{
			string normalizedUserId = userId.Trim();

			return _db.Transactions
				.AsNoTracking()
				.Where(transaction =>
					transaction.UserId == normalizedUserId &&
					transaction.Currency == WalletCurrency &&
					transaction.Status ==
						TransactionStatus.Complete &&
					(
						transaction.Type ==
							TransactionType.WalletTopUp ||
						transaction.Type ==
							TransactionType.WalletCharge
					));
		}

		private static void ValidateUserId(string? userId)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				throw new ArgumentException(
					"User ID is required.",
					nameof(userId));
			}
		}

		private static decimal ValidateAmount(decimal amount)
		{
			if (amount <= 0m)
			{
				throw new ArgumentOutOfRangeException(
					nameof(amount),
					"Amount must be greater than zero.");
			}

			return decimal.Round(
				amount,
				2,
				MidpointRounding.AwayFromZero);
		}

		private static string ValidateTitle(string? title)
		{
			if (string.IsNullOrWhiteSpace(title))
			{
				throw new ArgumentException(
					"Transaction title is required.",
					nameof(title));
			}

			string normalizedTitle = title.Trim();

			if (normalizedTitle.Length > MaximumTitleLength)
			{
				throw new ArgumentException(
					$"Transaction title cannot exceed {MaximumTitleLength} characters.",
					nameof(title));
			}

			return normalizedTitle;
		}
	}
}
