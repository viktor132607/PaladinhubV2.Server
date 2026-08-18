using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountOverviewService : IAccountOverviewService
	{
		private const string Currency = "USD";
		private const int PageSize = 5;

		private readonly AppDbContext _db;
		private readonly IAccountUiService _ui;
		private readonly IWalletService _wallet;

		public AccountOverviewService(
			AppDbContext db,
			IAccountUiService ui,
			IWalletService wallet)
		{
			_db = db;
			_ui = ui;
			_wallet = wallet;
		}

		public async Task<MyAccountViewModel?> GetMyAccountAsync(
			ClaimsPrincipal principal,
			CancellationToken cancellationToken = default)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null)
			{
				return null;
			}

			List<Transaction> recent = await _db.Transactions
				.AsNoTracking()
				.Where(transaction => transaction.UserId == me.Id)
				.OrderByDescending(transaction => transaction.CreatedAtUtc)
				.Take(PageSize)
				.ToListAsync(cancellationToken);

			return await BuildModelAsync(
				me,
				recent,
				1,
				1);
		}

		public async Task<MyAccountViewModel?> GetOverviewAsync(
			ClaimsPrincipal principal,
			int page,
			CancellationToken cancellationToken = default)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null)
			{
				return null;
			}

			page = Math.Max(page, 1);

			var query = _db.Transactions
				.AsNoTracking()
				.Where(transaction => transaction.UserId == me.Id)
				.OrderByDescending(transaction => transaction.CreatedAtUtc);

			int total = await query.CountAsync(cancellationToken);
			int totalPages = Math.Max(
				1,
				(int)Math.Ceiling(total / (double)PageSize));

			page = Math.Clamp(page, 1, totalPages);

			List<Transaction> recent = await query
				.Skip((page - 1) * PageSize)
				.Take(PageSize)
				.ToListAsync(cancellationToken);

			return await BuildModelAsync(
				me,
				recent,
				page,
				totalPages);
		}

		private async Task<MyAccountViewModel> BuildModelAsync(
			User me,
			List<Transaction> recent,
			int page,
			int totalPages)
		{
			decimal balance = await _wallet.GetBalanceAsync(me.Id);
			var (score, tips) = _ui.ComputeSecurityScore(me);

			return new MyAccountViewModel
			{
				Currency = Currency,
				Balance = balance,
				RecentPurchases = recent,
				Page = page,
				TotalPages = totalPages,
				SecurityScore = score,
				SecurityTips = tips,
				Uploads = _ui.GetUserUploadedAvatars(me.Id).ToList()
			};
		}
	}
}
