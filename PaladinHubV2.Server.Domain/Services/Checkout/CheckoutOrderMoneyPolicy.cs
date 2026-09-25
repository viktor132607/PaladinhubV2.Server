using PaladinHub.Models.Checkout;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public static class CheckoutOrderMoneyPolicy
{
    private const string DefaultCurrency = "EUR";
    private const string DefaultRegion = "EU";
    private const string UsdCurrency = "USD";
    private const string UsRegion = "US";

    public static decimal ResolveWalletTotal(
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

    public static CheckoutPurchaseMoney ResolvePurchase(
        CheckoutState state)
    {
        bool usdCard =
            state.PaymentMethod ==
                PaymentMethod.Card &&
            state.Currency == UsdCurrency;

        decimal amount =
            usdCard &&
            state.UsdPerEur > 0m
                ? ResolveWalletTotal(
                    state.Total,
                    state.UsdPerEur)
                : state.Total;

        return new CheckoutPurchaseMoney(
            amount,
            usdCard
                ? UsdCurrency
                : DefaultCurrency,
            usdCard
                ? UsRegion
                : DefaultRegion);
    }
}
