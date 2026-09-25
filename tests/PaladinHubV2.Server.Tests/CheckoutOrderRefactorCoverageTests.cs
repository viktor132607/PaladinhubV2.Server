using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Checkout;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;
using PaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Tests;

public sealed class CheckoutOrderRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task CartSnapshotProviderSyncsAndProjectsCart()
    {
        var cartSession = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        var user = new User { Id = "user-1" };

        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(
                new MyCartViewModel
                {
                    TotalPrice = 42.50m,
                    MyProducts =
                    [
                        new ProductViewModel
                        {
                            Id = "one",
                            Name = "One",
                            Price = 20m
                        },
                        new ProductViewModel
                        {
                            Id = "two",
                            Name = "Two",
                            Price = 22.50m
                        }
                    ]
                });

        var provider =
            new CheckoutCartSnapshotProvider(
                cartSession.Object,
                products.Object);

        CheckoutCartSnapshot snapshot =
            await provider.GetAsync(
                user,
                Ct);

        Assert.Equal(2, snapshot.Items);
        Assert.Equal(42.50m, snapshot.Total);

        cartSession.Verify(service =>
            service.SyncRedisToPersistent(
                user,
                Ct),
            Times.Once);
    }

    [Fact]
    public async Task CartSnapshotProviderHandlesNullProductCollection()
    {
        var cartSession = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        var user = new User { Id = "user-2" };

        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(
                new MyCartViewModel
                {
                    TotalPrice = 0m,
                    MyProducts = null!
                });

        var provider =
            new CheckoutCartSnapshotProvider(
                cartSession.Object,
                products.Object);

        CheckoutCartSnapshot snapshot =
            await provider.GetAsync(
                user,
                Ct);

        Assert.Equal(0, snapshot.Items);
        Assert.Equal(0m, snapshot.Total);
    }

    [Fact]
    public async Task WalletReviewSkipsNonBalancePayments()
    {
        var wallet = new Mock<IWalletService>();
        var service =
            new CheckoutWalletReviewService(
                wallet.Object);

        var state = new CheckoutState
        {
            PaymentMethod = PaymentMethod.Card,
            UsdPerEur = 9m
        };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                15m);

        Assert.Null(review.WalletBalance);
        Assert.Null(review.PaymentError);
        Assert.Equal(9m, state.UsdPerEur);

        wallet.Verify(
            item => item.GetBalanceAsync(
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WalletReviewWithoutRateProviderUsesEuroTotal()
    {
        var wallet = new Mock<IWalletService>();
        wallet.Setup(service =>
                service.GetBalanceAsync("user"))
            .ReturnsAsync(50m);

        var service =
            new CheckoutWalletReviewService(
                wallet.Object);

        var state = new CheckoutState
        {
            PaymentMethod = PaymentMethod.Balance
        };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                20m);

        Assert.False(service.RequiresVerifiedRate);
        Assert.Equal(50m, review.WalletBalance);
        Assert.Null(review.PaymentError);
        Assert.Equal(0m, state.UsdPerEur);
        Assert.Equal(
            20m,
            service.GetChargeAmount(state));
    }

    [Fact]
    public async Task WalletReviewUsesVerifiedRateAndDetectsInsufficientBalance()
    {
        var wallet = new Mock<IWalletService>();
        var rates = new Mock<IEuroUsdRateProvider>();

        wallet.Setup(service =>
                service.GetBalanceAsync("user"))
            .ReturnsAsync(10m);

        rates.Setup(service =>
                service.GetUsdPerEurAsync(
                    CancellationToken.None))
            .ReturnsAsync(1.25m);

        var service =
            new CheckoutWalletReviewService(
                wallet.Object,
                rates.Object);

        var state = new CheckoutState
        {
            PaymentMethod = PaymentMethod.Balance,
            Total = 20m
        };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                20m);

        Assert.True(service.RequiresVerifiedRate);
        Assert.Equal(1.25m, state.UsdPerEur);
        Assert.Equal(
            "Insufficient wallet balance.",
            review.PaymentError);
        Assert.Equal(
            25m,
            service.GetChargeAmount(state));
        Assert.Equal(
            25m,
            CheckoutWalletReviewService
                .ConvertToWalletAmount(
                    20m,
                    1.25m));
        Assert.Equal(
            20m,
            CheckoutWalletReviewService
                .ConvertToWalletAmount(
                    20m,
                    0m));
    }

    [Fact]
    public async Task WalletReviewMapsRateExceptionsAndInvalidRates()
    {
        var wallet = new Mock<IWalletService>();
        var rates = new Mock<IEuroUsdRateProvider>();

        wallet.Setup(service =>
                service.GetBalanceAsync("user"))
            .ReturnsAsync(100m);

        rates.SetupSequence(service =>
                service.GetUsdPerEurAsync(
                    CancellationToken.None))
            .ThrowsAsync(
                new HttpRequestException())
            .ReturnsAsync(0m);

        var service =
            new CheckoutWalletReviewService(
                wallet.Object,
                rates.Object);

        var firstState = new CheckoutState
        {
            PaymentMethod = PaymentMethod.Balance
        };

        CheckoutPaymentReview first =
            await service.ReviewAsync(
                new User { Id = "user" },
                firstState,
                10m);

        Assert.Contains(
            "exchange rate",
            first.PaymentError!,
            StringComparison.OrdinalIgnoreCase);

        var secondState = new CheckoutState
        {
            PaymentMethod = PaymentMethod.Balance
        };

        CheckoutPaymentReview second =
            await service.ReviewAsync(
                new User { Id = "user" },
                secondState,
                10m);

        Assert.Contains(
            "exchange rate",
            second.PaymentError!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransactionStoreCoversExistenceLoggingAndDuplicateGuard()
    {
        await using AppDbContext db =
            CreateDb();

        var store =
            new CheckoutTransactionStore(db);

        var user = new User
        {
            Id = "user-1"
        };

        var invalid = new CheckoutState
        {
            OrderId = "invalid",
            Total = 0m,
            PaymentMethod = PaymentMethod.CashOnDelivery
        };

        await store.LogPurchaseAsync(
            user,
            invalid,
            TransactionStatus.Pending,
            Ct);

        Assert.Empty(db.Transactions);

        var state = new CheckoutState
        {
            OrderId = "cash-order",
            Total = 19.99m,
            PaymentMethod = PaymentMethod.CashOnDelivery
        };

        await store.LogPurchaseAsync(
            user,
            state,
            TransactionStatus.Pending,
            Ct);

        Transaction transaction =
            Assert.Single(db.Transactions);

        Assert.Equal("cash-order", transaction.ExternalId);
        Assert.Equal(19.99m, transaction.Amount);
        Assert.Equal("EUR", transaction.Currency);
        Assert.Equal("EU", transaction.Region);
        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Equal(TransactionType.Purchase, transaction.Type);
        Assert.Contains(
            "CashOnDelivery",
            transaction.PurchaseTitle);

        Assert.True(
            await store.ExistsAsync(
                user.Id,
                "cash-order",
                Ct));

        await store.LogPurchaseAsync(
            user,
            state,
            TransactionStatus.Complete,
            Ct);

        Assert.Single(db.Transactions);
    }

    [Fact]
    public async Task TransactionStoreLogsUsdCardAndAttachesWalletMetadata()
    {
        await using AppDbContext db =
            CreateDb();

        var store =
            new CheckoutTransactionStore(db);
        var user = new User { Id = "user-2" };

        var state = new CheckoutState
        {
            OrderId = "card-order",
            Total = 10m,
            PaymentMethod = PaymentMethod.Card,
            Currency = "USD",
            UsdPerEur = 1.234m
        };

        await store.LogPurchaseAsync(
            user,
            state,
            TransactionStatus.Complete,
            Ct);

        Transaction purchase =
            Assert.Single(db.Transactions);

        Assert.Equal(12.34m, purchase.Amount);
        Assert.Equal("USD", purchase.Currency);
        Assert.Equal("US", purchase.Region);

        var walletCharge = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Amount = -5m,
            Currency = "USD",
            Region = "US",
            Status = TransactionStatus.Complete,
            Type = TransactionType.WalletCharge
        };

        db.Transactions.Add(walletCharge);
        await db.SaveChangesAsync(Ct);

        await store.AttachOrderMetadataAsync(
            walletCharge.Id,
            "wallet-order",
            Ct);

        Assert.Equal(
            "wallet-order",
            walletCharge.ExternalId);
        Assert.Equal("EU", walletCharge.Region);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AttachOrderMetadataAsync(
                Guid.NewGuid(),
                "missing",
                Ct));
    }

    [Fact]
    public async Task FacadeDelegatesReadOperations()
    {
        var snapshots =
            new Mock<ICheckoutCartSnapshotProvider>();
        var review =
            new Mock<ICheckoutWalletReviewService>();
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var wallet =
            new Mock<IWalletService>();
        var cart =
            new Mock<ICartSessionService>();

        var user = new User { Id = "user" };
        var state = new CheckoutState();

        snapshots.Setup(service =>
                service.GetAsync(
                    user,
                    Ct))
            .ReturnsAsync(
                new CheckoutCartSnapshot(
                    2,
                    30m));

        review.Setup(service =>
                service.ReviewAsync(
                    user,
                    state,
                    30m))
            .ReturnsAsync(
                new CheckoutPaymentReview(
                    100m,
                    null));

        transactions.Setup(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(true);

        CheckoutOrderService service =
            CreateService(
                snapshots.Object,
                review.Object,
                transactions.Object,
                wallet.Object,
                cart.Object);

        Assert.Equal(
            30m,
            (await service.GetCartSnapshotAsync(
                user,
                Ct)).Total);

        Assert.Equal(
            100m,
            (await service.GetPaymentReviewAsync(
                user,
                state,
                30m)).WalletBalance);

        Assert.True(
            await service.OrderTransactionExistsAsync(
                user.Id,
                "order",
                Ct));
    }

    [Fact]
    public async Task CashOnDeliveryLogsOnlyNewOrderAndAlwaysArchives()
    {
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var cart =
            new Mock<ICartSessionService>();
        var user = new User { Id = "user" };
        var state = new CheckoutState
        {
            OrderId = "order",
            Total = 20m,
            PaymentMethod = PaymentMethod.CashOnDelivery
        };

        transactions.SetupSequence(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        CheckoutOrderService service =
            CreateService(
                transactions: transactions.Object,
                cartSession: cart.Object);

        CheckoutOrderPlacementResult first =
            await service.PlaceCashOnDeliveryAsync(
                user,
                state,
                "order",
                Ct);

        CheckoutOrderPlacementResult second =
            await service.PlaceCashOnDeliveryAsync(
                user,
                state,
                "order",
                Ct);

        Assert.True(first.Success);
        Assert.True(second.Success);

        transactions.Verify(service =>
            service.LogPurchaseAsync(
                user,
                state,
                TransactionStatus.Pending,
                Ct),
            Times.Once);

        cart.Verify(service =>
            service.ArchiveAndClear(
                user,
                Ct),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WalletPlacementRequiresRateWhenConfigured()
    {
        var review =
            new Mock<ICheckoutWalletReviewService>();
        var transactions =
            new Mock<ICheckoutTransactionStore>();

        review.SetupGet(service =>
                service.RequiresVerifiedRate)
            .Returns(true);

        transactions.Setup(service =>
                service.ExistsAsync(
                    "user",
                    "order",
                    Ct))
            .ReturnsAsync(false);

        CheckoutOrderService service =
            CreateService(
                review: review.Object,
                transactions: transactions.Object);

        CheckoutOrderPlacementResult result =
            await service.PlaceWalletAsync(
                new User { Id = "user" },
                new CheckoutState
                {
                    Total = 10m,
                    UsdPerEur = 0m
                },
                "order",
                Ct);

        Assert.False(result.Success);
        Assert.Contains(
            "exchange rate",
            result.ErrorMessage!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WalletPlacementMapsChargeFailure()
    {
        var review =
            new Mock<ICheckoutWalletReviewService>();
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var wallet =
            new Mock<IWalletService>();

        var state = new CheckoutState
        {
            Total = 10m,
            UsdPerEur = 1.2m
        };

        transactions.Setup(service =>
                service.ExistsAsync(
                    "user",
                    "order",
                    Ct))
            .ReturnsAsync(false);

        review.SetupGet(service =>
                service.RequiresVerifiedRate)
            .Returns(true);

        review.Setup(service =>
                service.GetChargeAmount(
                    state))
            .Returns(12m);

        wallet.Setup(service =>
                service.ChargeAsync(
                    "user",
                    12m,
                    "Order order (Wallet)"))
            .ThrowsAsync(
                new InvalidOperationException());

        CheckoutOrderService service =
            CreateService(
                review: review.Object,
                transactions: transactions.Object,
                wallet: wallet.Object);

        CheckoutOrderPlacementResult result =
            await service.PlaceWalletAsync(
                new User { Id = "user" },
                state,
                "order",
                Ct);

        Assert.False(result.Success);
        Assert.Equal(
            "Insufficient wallet balance.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task WalletPlacementChargesAttachesMetadataAndArchives()
    {
        var review =
            new Mock<ICheckoutWalletReviewService>();
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var wallet =
            new Mock<IWalletService>();
        var cart =
            new Mock<ICartSessionService>();

        var user = new User { Id = "user" };
        var state = new CheckoutState
        {
            Total = 10m
        };
        Guid transactionId =
            Guid.NewGuid();

        transactions.Setup(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(false);

        review.SetupGet(service =>
                service.RequiresVerifiedRate)
            .Returns(false);

        review.Setup(service =>
                service.GetChargeAmount(
                    state))
            .Returns(10m);

        wallet.Setup(service =>
                service.ChargeAsync(
                    user.Id,
                    10m,
                    "Order order (Wallet)"))
            .ReturnsAsync(transactionId);

        CheckoutOrderService service =
            CreateService(
                review: review.Object,
                transactions: transactions.Object,
                wallet: wallet.Object,
                cartSession: cart.Object);

        CheckoutOrderPlacementResult result =
            await service.PlaceWalletAsync(
                user,
                state,
                "order",
                Ct);

        Assert.True(result.Success);

        transactions.Verify(service =>
            service.AttachOrderMetadataAsync(
                transactionId,
                "order",
                Ct),
            Times.Once);

        cart.Verify(service =>
            service.ArchiveAndClear(
                user,
                Ct),
            Times.Once);
    }

    [Fact]
    public async Task WalletPlacementAlreadyProcessedSkipsChargeAndArchives()
    {
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var wallet =
            new Mock<IWalletService>();
        var cart =
            new Mock<ICartSessionService>();
        var user = new User { Id = "user" };

        transactions.Setup(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(true);

        CheckoutOrderService service =
            CreateService(
                transactions: transactions.Object,
                wallet: wallet.Object,
                cartSession: cart.Object);

        CheckoutOrderPlacementResult result =
            await service.PlaceWalletAsync(
                user,
                new CheckoutState(),
                "order",
                Ct);

        Assert.True(result.Success);

        wallet.Verify(service =>
            service.ChargeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>()),
            Times.Never);

        cart.Verify(service =>
            service.ArchiveAndClear(
                user,
                Ct),
            Times.Once);
    }

    [Fact]
    public async Task CardCompletionLogsCompletePurchaseAndArchives()
    {
        var transactions =
            new Mock<ICheckoutTransactionStore>();
        var cart =
            new Mock<ICartSessionService>();
        var user = new User { Id = "user" };
        var state = new CheckoutState
        {
            OrderId = "card-order",
            Total = 50m,
            PaymentMethod = PaymentMethod.Card
        };

        CheckoutOrderService service =
            CreateService(
                transactions: transactions.Object,
                cartSession: cart.Object);

        await service.CompleteCardOrderAsync(
            user,
            state,
            Ct);

        transactions.Verify(service =>
            service.LogPurchaseAsync(
                user,
                state,
                TransactionStatus.Complete,
                Ct),
            Times.Once);

        await service.ArchiveCartAsync(
            user,
            Ct);

        cart.Verify(service =>
            service.ArchiveAndClear(
                user,
                Ct),
            Times.Exactly(2));
    }

    private static CheckoutOrderService CreateService(
        ICheckoutCartSnapshotProvider? snapshots = null,
        ICheckoutWalletReviewService? review = null,
        ICheckoutTransactionStore? transactions = null,
        IWalletService? wallet = null,
        ICartSessionService? cartSession = null)
    {
        return new CheckoutOrderService(
            snapshots ??
                Mock.Of<ICheckoutCartSnapshotProvider>(),
            review ??
                Mock.Of<ICheckoutWalletReviewService>(),
            transactions ??
                Mock.Of<ICheckoutTransactionStore>(),
            wallet ??
                Mock.Of<IWalletService>(),
            cartSession ??
                Mock.Of<ICartSessionService>());
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "checkout-order-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
