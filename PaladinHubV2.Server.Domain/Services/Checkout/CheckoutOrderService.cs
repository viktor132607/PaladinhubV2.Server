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
    private readonly ICheckoutCartCoordinator _cart;
    private readonly ICheckoutWalletPaymentService _walletPayments;
    private readonly ICheckoutOrderTransactionService _transactions;

    public CheckoutOrderService(
        ICartSessionService cartSession,
        IProductService productService,
        IWalletService wallet,
        AppDbContext db,
        IEuroUsdRateProvider? rates = null)
    {
        _transactions =
            new CheckoutOrderTransactionService(db);

        _cart =
            new CheckoutCartCoordinator(
                cartSession,
                productService);

        _walletPayments =
            new CheckoutWalletPaymentService(
                wallet,
                _transactions,
                rates);
    }

    public CheckoutOrderService(
        ICheckoutCartCoordinator cart,
        ICheckoutWalletPaymentService walletPayments,
        ICheckoutOrderTransactionService transactions)
    {
        _cart = cart;
        _walletPayments = walletPayments;
        _transactions = transactions;
    }

    public Task<CheckoutCartSnapshot> GetCartSnapshotAsync(
        User user,
        CancellationToken cancellationToken)
    {
        return _cart.GetSnapshotAsync(
            user,
            cancellationToken);
    }

    public Task<CheckoutPaymentReview> GetPaymentReviewAsync(
        User user,
        CheckoutState state,
        decimal total)
    {
        return _walletPayments.ReviewAsync(
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

        await _cart.ArchiveAsync(
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
            CheckoutOrderPlacementResult charge =
                await _walletPayments.ChargeAsync(
                    user,
                    state,
                    orderId,
                    cancellationToken);

            if (!charge.Success)
            {
                return charge;
            }
        }

        await _cart.ArchiveAsync(
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

        await _cart.ArchiveAsync(
            user,
            cancellationToken);
    }

    public Task ArchiveCartAsync(
        User user,
        CancellationToken cancellationToken)
    {
        return _cart.ArchiveAsync(
            user,
            cancellationToken);
    }
}
