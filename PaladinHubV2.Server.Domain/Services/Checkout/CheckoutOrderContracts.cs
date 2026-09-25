using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public readonly record struct CheckoutCartSnapshot(
    int Items,
    decimal Total);

public sealed record CheckoutPaymentReview(
    decimal? WalletBalance,
    string? PaymentError);

public sealed record CheckoutOrderPlacementResult(
    bool Success,
    string? ErrorMessage = null);

public readonly record struct CheckoutPurchaseMoney(
    decimal Amount,
    string Currency,
    string Region);

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

public interface ICheckoutCartCoordinator
{
    Task<CheckoutCartSnapshot> GetSnapshotAsync(
        User user,
        CancellationToken cancellationToken);

    Task ArchiveAsync(
        User user,
        CancellationToken cancellationToken);
}

public interface ICheckoutWalletPaymentService
{
    Task<CheckoutPaymentReview> ReviewAsync(
        User user,
        CheckoutState state,
        decimal total);

    Task<CheckoutOrderPlacementResult> ChargeAsync(
        User user,
        CheckoutState state,
        string orderId,
        CancellationToken cancellationToken);
}

public interface ICheckoutOrderTransactionService
{
    Task<bool> ExistsAsync(
        string userId,
        string orderId,
        CancellationToken cancellationToken);

    Task LogPurchaseAsync(
        User user,
        CheckoutState state,
        TransactionStatus status,
        CancellationToken cancellationToken);

    Task AttachOrderMetadataAsync(
        Guid transactionId,
        string orderId,
        CancellationToken cancellationToken);
}
