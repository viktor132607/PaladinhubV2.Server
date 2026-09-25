using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public sealed class CheckoutOrderService :
    ICheckoutOrderService
{
    private const string RateUnavailableMessage =
        "Wallet payments are temporarily unavailable while the exchange rate cannot be verified.";

    private const string InsufficientBalanceMessage =
        "Insufficient wallet balance.";

    private readonly ICheckoutCartSnapshotProvider _snapshots;
    private readonly ICheckoutWalletReviewService _walletReview;
    private readonly ICheckoutTransactionStore _transactions;
    private readonly IWalletService _wallet;
    private readonly ICartSessionService _cartSession;

    public CheckoutOrderService(
        ICartSessionService cartSession,
        IProductService productService,
        IWalletService wallet,
        AppDbContext db,
        IEuroUsdRateProvider? rates = null)
        : this(
            new CheckoutCartSnapshotProvider(
                cartSession,
                productService),
            new CheckoutWalletReviewService(
                wallet,
                rates),
            new CheckoutTransactionStore(db),
            wallet,
            cartSession)
    {
    }

    public CheckoutOrderService(
        ICheckoutCartSnapshotProvider snapshots,
        ICheckoutWalletReviewService walletReview,
        ICheckoutTransactionStore transactions,
        IWalletService wallet,
        ICartSessionService cartSession)
    {
        _snapshots = snapshots;
        _walletReview = walletReview;
        _transactions = transactions;
        _wallet = wallet;
        _cartSession = cartSession;
    }

    public Task<CheckoutCartSnapshot> GetCartSnapshotAsync(
        User user,
        CancellationToken cancellationToken)
    {
        return _snapshots.GetAsync(
            user,
            cancellationToken);
    }

    public Task<CheckoutPaymentReview> GetPaymentReviewAsync(
        User user,
        CheckoutState state,
        decimal total)
    {
        return _walletReview.ReviewAsync(
            user,
            state,
            total);
    }

    public Task<bool> OrderTransactionExistsAsync(
        string userId,
        string orderId,
        CancellationToken cancellationToken)
    {
        return _transactions.ExistsAsync(
            userId,
            orderId,
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
            await _transactions.ExistsAsync(
                user.Id,
                orderId,
                cancellationToken);

        if (!alreadyProcessed)
        {
            await _transactions.LogPurchaseAsync(
                user,
                state,
                TransactionStatus.Pending,
                cancellationToken);
        }

        await ArchiveCartAsync(
            user,
            cancellationToken);

        return new CheckoutOrderPlacementResult(
            true);
    }

    public async Task<CheckoutOrderPlacementResult>
        PlaceWalletAsync(
            User user,
            CheckoutState state,
            string orderId,
            CancellationToken cancellationToken)
    {
        bool alreadyProcessed =
            await _transactions.ExistsAsync(
                user.Id,
                orderId,
                cancellationToken);

        if (!alreadyProcessed)
        {
            if (_walletReview.RequiresVerifiedRate &&
                state.UsdPerEur <= 0m)
            {
                return new CheckoutOrderPlacementResult(
                    false,
                    RateUnavailableMessage);
            }

            decimal walletTotal =
                _walletReview.GetChargeAmount(
                    state);

            try
            {
                Guid transactionId =
                    await _wallet.ChargeAsync(
                        user.Id,
                        walletTotal,
                        $"Order {orderId} (Wallet)");

                await _transactions.AttachOrderMetadataAsync(
                    transactionId,
                    orderId,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return new CheckoutOrderPlacementResult(
                    false,
                    InsufficientBalanceMessage);
            }
        }

        await ArchiveCartAsync(
            user,
            cancellationToken);

        return new CheckoutOrderPlacementResult(
            true);
    }

    public async Task CompleteCardOrderAsync(
        User user,
        CheckoutState state,
        CancellationToken cancellationToken)
    {
        await _transactions.LogPurchaseAsync(
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
}
