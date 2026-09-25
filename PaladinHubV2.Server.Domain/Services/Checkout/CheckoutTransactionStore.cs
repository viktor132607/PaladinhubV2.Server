using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public sealed class CheckoutTransactionStore :
    ICheckoutTransactionStore
{
    private const string DefaultCurrency = "EUR";
    private const string DefaultRegion = "EU";

    private readonly AppDbContext _db;

    public CheckoutTransactionStore(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(
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

    public async Task LogPurchaseAsync(
        User user,
        CheckoutState state,
        TransactionStatus status,
        CancellationToken cancellationToken)
    {
        if (state.Total <= 0m ||
            string.IsNullOrWhiteSpace(
                state.OrderId))
        {
            return;
        }

        if (await ExistsAsync(
                user.Id,
                state.OrderId,
                cancellationToken))
        {
            return;
        }

        bool isUsdCard =
            state.PaymentMethod ==
                CheckoutPaymentMethod.Card &&
            state.Currency == "USD";

        decimal amount =
            isUsdCard &&
            state.UsdPerEur > 0m
                ? decimal.Round(
                    state.Total * state.UsdPerEur,
                    2,
                    MidpointRounding.AwayFromZero)
                : state.Total;

        _db.Transactions.Add(
            new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow,
                PurchaseTitle =
                    $"Order {state.OrderId} " +
                    $"({state.PaymentMethod})",
                Amount = amount,
                Currency =
                    isUsdCard
                        ? "USD"
                        : DefaultCurrency,
                Region =
                    isUsdCard
                        ? "US"
                        : DefaultRegion,
                Status = status,
                ExternalId = state.OrderId,
                Type = TransactionType.Purchase
            });

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    public async Task AttachOrderMetadataAsync(
        Guid transactionId,
        string orderId,
        CancellationToken cancellationToken)
    {
        Transaction? transaction =
            await _db.Transactions
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                        transactionId,
                    cancellationToken);

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "Wallet transaction was not found.");
        }

        transaction.ExternalId = orderId;
        transaction.Region = DefaultRegion;

        await _db.SaveChangesAsync(
            cancellationToken);
    }
}
