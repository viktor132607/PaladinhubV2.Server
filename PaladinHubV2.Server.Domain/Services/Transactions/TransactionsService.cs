using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Transactions
{
	public sealed class TransactionsService : ITransactionsService
	{
		private const int DefaultPageSize = 10;
		private const int MaximumPageSize = 100;
		private const string DefaultRegion = "Europe";

		private readonly AppDbContext _db;

		public TransactionsService(AppDbContext db)
		{
			_db = db;
		}

		public Task<TransactionHistoryViewModel> GetHistoryForRequest(
			string userId,
			string? region,
			int page,
			int pageSize)
		{
			int normalizedPage =
				Math.Max(1, page);

			int normalizedPageSize =
				Math.Clamp(
					pageSize,
					1,
					MaximumPageSize);

			string normalizedRegion =
				string.IsNullOrWhiteSpace(region)
					? DefaultRegion
					: region.Trim();

			return GetHistory(
				userId,
				normalizedRegion,
				normalizedPage,
				normalizedPageSize);
		}

		public async Task<TransactionHistoryViewModel> GetHistory(
			string userId,
			string region,
			int page,
			int pageSize)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				throw new ArgumentException(
					"User ID is required.",
					nameof(userId));
			}

			string normalizedUserId = userId.Trim();
			string normalizedRegion = NormalizeRegion(region);

			pageSize = pageSize <= 0
				? DefaultPageSize
				: Math.Min(pageSize, MaximumPageSize);

			var query = _db.Transactions
				.AsNoTracking()
				.Where(transaction =>
					transaction.UserId == normalizedUserId);

			query = ApplyRegionFilter(
				query,
				normalizedRegion);

			int totalItems = await query.CountAsync();

			int totalPages = Math.Max(
				1,
				(int)Math.Ceiling(
					totalItems / (double)pageSize));

			page = Math.Clamp(
				page,
				1,
				totalPages);

			var rawItems = await query
				.OrderByDescending(transaction =>
					transaction.CreatedAtUtc)
				.ThenByDescending(transaction =>
					transaction.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(transaction => new
				{
					transaction.CreatedAtUtc,
					transaction.PurchaseTitle,
					transaction.Amount,
					transaction.Currency,
					transaction.Status
				})
				.ToListAsync();

			var items = rawItems
				.Select(transaction =>
					new TransactionHistoryItemVm
					{
						DateUtc =
							transaction.CreatedAtUtc,

						Purchase =
							transaction.PurchaseTitle,

						Total = FormatCurrency(
							transaction.Currency,
							transaction.Amount),

						Status =
							transaction.Status.ToString()
					})
				.ToList();

			return new TransactionHistoryViewModel
			{
				Items = items,
				Page = page,
				TotalPages = totalPages,
				Region = normalizedRegion
			};
		}

		private static IQueryable<Transaction> ApplyRegionFilter(
			IQueryable<Transaction> query,
			string region)
		{
			return region switch
			{
				"Europe" => query.Where(transaction =>
					EF.Functions.ILike(
						transaction.Region,
						"Europe") ||
					EF.Functions.ILike(
						transaction.Region,
						"EU")),

				"Americas" => query.Where(transaction =>
					EF.Functions.ILike(
						transaction.Region,
						"Americas") ||
					EF.Functions.ILike(
						transaction.Region,
						"America") ||
					EF.Functions.ILike(
						transaction.Region,
						"US") ||
					EF.Functions.ILike(
						transaction.Region,
						"USA")),

				"Asia" => query.Where(transaction =>
					EF.Functions.ILike(
						transaction.Region,
						"Asia") ||
					EF.Functions.ILike(
						transaction.Region,
						"APAC")),

				_ => query
			};
		}

		private static string NormalizeRegion(string? region)
		{
			if (string.IsNullOrWhiteSpace(region))
			{
				return DefaultRegion;
			}

			return region.Trim().ToUpperInvariant() switch
			{
				"EUROPE" or "EU" or "EUR" =>
					"Europe",

				"AMERICAS" or
				"AMERICA" or
				"US" or
				"USA" or
				"NORTH AMERICA" or
				"SOUTH AMERICA" =>
					"Americas",

				"ASIA" or "APAC" =>
					"Asia",

				"ALL" =>
					"All",

				_ =>
					"All"
			};
		}

		private static string FormatCurrency(
			string? currencyCode,
			decimal amount)
		{
			string code =
				currencyCode?.Trim().ToUpperInvariant() ??
				string.Empty;

			return code switch
			{
				"EUR" => $"€{amount:0.00}",
				"USD" => $"${amount:0.00}",
				"GBP" => $"£{amount:0.00}",
				"BGN" => $"BGN{amount:0.00}",
				_ when string.IsNullOrEmpty(code) =>
					$"{amount:0.00}",
				_ => $"{code}{amount:0.00}"
			};
		}
	}
}
