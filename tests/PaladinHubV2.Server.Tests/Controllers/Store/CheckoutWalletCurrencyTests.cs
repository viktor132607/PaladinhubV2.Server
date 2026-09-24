using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Checkout;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CheckoutWalletCurrencyTests
{
    [Theory]
    [InlineData(105, true)]
    [InlineData(120, false)]
    public async Task Review_ComparesUsdWalletAgainstConvertedEurTotal(decimal balance, bool insufficient)
    {
        using var db = Context();
        var wallet = new Mock<IWalletService>();
        wallet.Setup(x => x.GetBalanceAsync("user-1")).ReturnsAsync(balance);
        var rates = new Mock<IEuroUsdRateProvider>();
        rates.Setup(x => x.GetUsdPerEurAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1.2m);
        var orders = Service(db, wallet, rates);
        var state = new CheckoutState { PaymentMethod = PaymentMethod.Balance };

        CheckoutPaymentReview review = await orders.GetPaymentReviewAsync(new User { Id = "user-1" }, state, 100m);

        Assert.Equal(balance, review.WalletBalance);
        Assert.Equal(1.2m, state.UsdPerEur);
        Assert.Equal(insufficient, review.PaymentError == "Insufficient wallet balance.");
    }

    [Fact]
    public async Task Review_WhenRateUnavailable_BlocksUsdWalletPayment()
    {
        using var db = Context();
        var wallet = new Mock<IWalletService>();
        wallet.Setup(x => x.GetBalanceAsync("user-1")).ReturnsAsync(200m);
        var rates = new Mock<IEuroUsdRateProvider>();
        rates.Setup(x => x.GetUsdPerEurAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var orders = Service(db, wallet, rates);
        var state = new CheckoutState { PaymentMethod = PaymentMethod.Balance };

        CheckoutPaymentReview review = await orders.GetPaymentReviewAsync(new User { Id = "user-1" }, state, 100m);

        Assert.Contains("exchange rate", review.PaymentError);
        Assert.Equal(0m, state.UsdPerEur);
    }

    private static CheckoutOrderService Service(AppDbContext db, Mock<IWalletService> wallet, Mock<IEuroUsdRateProvider> rates) =>
        new(new Mock<ICartSessionService>().Object, new Mock<IProductService>().Object, wallet.Object, db, rates.Object);

    private static AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"wallet-currency-{Guid.NewGuid():N}").Options);
}
