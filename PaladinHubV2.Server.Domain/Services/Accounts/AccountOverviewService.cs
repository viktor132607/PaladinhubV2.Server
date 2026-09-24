using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountOverviewService : IAccountOverviewService
{
    private const string Currency = "USD";
    private const int OverviewPageSize = 5;

    private readonly IWalletService _wallet;
    private readonly AppDbContext _db;
    private readonly IAccountSecurityScorer _security;
    private readonly IAccountAvatarService _avatars;

    public AccountOverviewService(
        IWalletService wallet,
        AppDbContext db,
        IAccountSecurityScorer security,
        IAccountAvatarService avatars)
    {
        _wallet = wallet;
        _db = db;
        _security = security;
        _avatars = avatars;
    }

    public async Task<MyAccountViewModel> BuildMyAccountAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        decimal balance =
            await _wallet.GetBalanceAsync(user.Id);

        List<Transaction> recent =
            await _db.Transactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.UserId == user.Id)
                .OrderByDescending(transaction =>
                    transaction.CreatedAtUtc)
                .Take(OverviewPageSize)
                .ToListAsync(cancellationToken);

        return BuildModel(
            user,
            balance,
            recent,
            page: 1,
            totalPages: 1);
    }

    public async Task<MyAccountViewModel> BuildOverviewAsync(
        User user,
        int page,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);

        IOrderedQueryable<Transaction> query =
            _db.Transactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.UserId == user.Id)
                .OrderByDescending(transaction =>
                    transaction.CreatedAtUtc);

        int total =
            await query.CountAsync(cancellationToken);

        int totalPages = Math.Max(
            1,
            (int)Math.Ceiling(
                total / (double)OverviewPageSize));

        page = Math.Clamp(page, 1, totalPages);

        List<Transaction> recent =
            await query
                .Skip((page - 1) * OverviewPageSize)
                .Take(OverviewPageSize)
                .ToListAsync(cancellationToken);

        decimal balance =
            await _wallet.GetBalanceAsync(user.Id);

        return BuildModel(
            user,
            balance,
            recent,
            page,
            totalPages);
    }

    public Task<decimal> GetBalanceAsync(string userId) =>
        _wallet.GetBalanceAsync(userId);

    private MyAccountViewModel BuildModel(
        User user,
        decimal balance,
        List<Transaction> recent,
        int page,
        int totalPages)
    {
        var (score, tips) = _security.Compute(user);

        return new MyAccountViewModel
        {
            Currency = Currency,
            Balance = balance,
            RecentPurchases = recent,
            Page = page,
            TotalPages = totalPages,
            SecurityScore = score,
            SecurityTips = tips,
            Uploads = _avatars
                .GetUserUploadedAvatars(user.Id)
                .ToList()
        };
    }
}
