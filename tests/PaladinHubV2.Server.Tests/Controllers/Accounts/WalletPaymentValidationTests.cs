using System.Reflection;
using PaladinHubV2.Server.API.Controllers.Accounts;
using Stripe.Checkout;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class WalletPaymentValidationTests
{
    [Fact]
    public void CompletedPaidWalletSession_IsEligibleForCredit() => Assert.True(Eligible(Paid()));

    [Theory]
    [InlineData("unpaid")]
    [InlineData("no_payment_required")]
    public void SessionWithoutSettledPayment_CannotCreditWallet(string status)
    {
        var session = Paid(); session.PaymentStatus = status;
        Assert.False(Eligible(session));
    }

    [Fact]
    public void MerchandiseCheckout_CannotCreditWallet()
    {
        var session = Paid(); session.Metadata.Clear();
        Assert.False(Eligible(session));
    }

    [Fact]
    public void MissingOwner_CannotCreditWallet()
    {
        var session = Paid(); session.ClientReferenceId = "";
        Assert.False(Eligible(session));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100001)]
    public void AmountOutsideWalletLimits_CannotCreditWallet(long amount)
    {
        var session = Paid(); session.AmountTotal = amount;
        Assert.False(Eligible(session));
    }

    [Fact]
    public void DifferentCurrency_CannotCreditDollarWallet()
    {
        var session = Paid(); session.Currency = "eur";
        Assert.False(Eligible(session));
    }

    private static Session Paid() => new()
    {
        Id = "cs_test", ClientReferenceId = "user-1", Mode = "payment", PaymentStatus = "paid",
        Currency = "usd", AmountTotal = 1000, Metadata = new() { ["purpose"] = "wallet-top-up" }
    };
    private static bool Eligible(Session session) => (bool)typeof(WalletPaymentsController)
        .GetMethod("IsPaidWalletSession", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session])!;
}
