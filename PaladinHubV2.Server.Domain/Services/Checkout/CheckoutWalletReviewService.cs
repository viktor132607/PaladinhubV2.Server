using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Wallet;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public sealed class CheckoutWalletReviewService :
    ICheckoutWalletReviewService
{
    private const string RateUnavailableMessage =
        "Wallet payments are temporarily unavailable while the exchange rate cannot be verified.";

    private readonly IWalletService _wallet;
    private readonly IEuroUsdRateProvider? _rates;

    public CheckoutWalletReviewService(
        IWalletService wallet,
        IEuroUsdRateProvider? rates = null)
    {
        _wallet = wallet;
        _rates = rates;
    }

    public bool RequiresVerifiedRate =>
        _rates is not null;

    public async Task<CheckoutPaymentReview> ReviewAsync(
        User user,
        CheckoutState state,
        decimal total)
    {
        if (state.PaymentMethod != CheckoutPaymentMethod.Balance)
        {
            return new CheckoutPaymentReview(
                null,
                null);
        }

        decimal walletBalance =
            await _wallet.GetBalanceAsync(user.Id);

        state.UsdPerEur = 0m;
        string? paymentError = null;

        try
        {
            state.UsdPerEur =
                _rates is null
                    ? 0m
                    : await _rates.GetUsdPerEurAsync(
                        CancellationToken.None);
        }
        catch (Exception error) when (
            error is HttpRequestException or
            TaskCanceledException or
            InvalidDataException or
            System.Xml.XmlException)
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
            ConvertToWalletAmount(
                total,
                state.UsdPerEur);

        if (paymentError is null &&
            walletBalance < walletTotal)
        {
            paymentError =
                "Insufficient wallet balance.";
        }

        return new CheckoutPaymentReview(
            walletBalance,
            paymentError);
    }

    public decimal GetChargeAmount(
        CheckoutState state)
    {
        return ConvertToWalletAmount(
            state.Total,
            state.UsdPerEur);
    }

    internal static decimal ConvertToWalletAmount(
        decimal total,
        decimal usdPerEur)
    {
        return usdPerEur > 0m
            ? decimal.Round(
                total * usdPerEur,
                2,
                MidpointRounding.AwayFromZero)
            : total;
    }
}
