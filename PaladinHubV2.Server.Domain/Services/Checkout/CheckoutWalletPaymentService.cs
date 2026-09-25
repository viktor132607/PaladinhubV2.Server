using System.Xml;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public sealed class CheckoutWalletPaymentService :
    ICheckoutWalletPaymentService
{
    private const string RateUnavailableMessage =
        "Wallet payments are temporarily unavailable while the exchange rate cannot be verified.";

    private const string InsufficientBalanceMessage =
        "Insufficient wallet balance.";

    private readonly IWalletService _wallet;
    private readonly IEuroUsdRateProvider? _rates;
    private readonly ICheckoutOrderTransactionService _transactions;

    public CheckoutWalletPaymentService(
        IWalletService wallet,
        ICheckoutOrderTransactionService transactions,
        IEuroUsdRateProvider? rates = null)
    {
        _wallet = wallet;
        _transactions = transactions;
        _rates = rates;
    }

    public async Task<CheckoutPaymentReview> ReviewAsync(
        User user,
        CheckoutState state,
        decimal total)
    {
        decimal? walletBalance = null;
        string? paymentError = null;

        if (state.PaymentMethod != CheckoutPaymentMethod.Balance)
        {
            return new CheckoutPaymentReview(
                walletBalance,
                paymentError);
        }

        walletBalance =
            await _wallet.GetBalanceAsync(
                user.Id);

        state.UsdPerEur = 0m;

        try
        {
            state.UsdPerEur =
                _rates is null
                    ? 0m
                    : await _rates.GetUsdPerEurAsync(
                        CancellationToken.None);
        }
        catch (Exception error)
            when (error is
                HttpRequestException or
                TaskCanceledException or
                InvalidDataException or
                XmlException)
        {
            paymentError =
                RateUnavailableMessage;
        }

        if (_rates is not null &&
            state.UsdPerEur <= 0m)
        {
            paymentError =
                RateUnavailableMessage;
        }

        decimal walletTotal =
            CheckoutOrderMoneyPolicy.ResolveWalletTotal(
                total,
                state.UsdPerEur);

        if (paymentError is null &&
            walletBalance < walletTotal)
        {
            paymentError =
                InsufficientBalanceMessage;
        }

        return new CheckoutPaymentReview(
            walletBalance,
            paymentError);
    }

    public async Task<CheckoutOrderPlacementResult> ChargeAsync(
        User user,
        CheckoutState state,
        string orderId,
        CancellationToken cancellationToken)
    {
        if (_rates is not null &&
            state.UsdPerEur <= 0m)
        {
            return new CheckoutOrderPlacementResult(
                false,
                RateUnavailableMessage);
        }

        decimal walletTotal =
            CheckoutOrderMoneyPolicy.ResolveWalletTotal(
                state.Total,
                state.UsdPerEur);

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

        return new CheckoutOrderPlacementResult(
            true);
    }
}
