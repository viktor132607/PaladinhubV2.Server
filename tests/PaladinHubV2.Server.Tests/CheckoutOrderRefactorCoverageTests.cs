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
    public async Task CartCoordinatorSyncsProjectsAndArchives()
    {
        var cartSession = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        var user = new User { Id = "user-1" };

        products.Setup(service =>
                service.GetMyProducts(user))
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

        var coordinator =
            new CheckoutCartCoordinator(
                cartSession.Object,
                products.Object);

        CheckoutCartSnapshot snapshot =
            await coordinator.GetSnapshotAsync(
                user,
                Ct);

        Assert.Equal(2, snapshot.Items);
        Assert.Equal(42.50m, snapshot.Total);

        await coordinator.ArchiveAsync(
            user,
            Ct);

        cartSession.Verify(service =>
            service.SyncRedisToPersistent(
                user,
                Ct),
            Times.Once);

        cartSession.Verify(service =>
            service.ArchiveAndClear(
                user,
                Ct),
            Times.Once);
    }

    [Fact]
    public async Task CartCoordinatorHandlesNullProductCollection()
    {
        var cartSession = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        var user = new User { Id = "user-2" };

        products.Setup(service =>
                service.GetMyProducts(user))
            .ReturnsAsync(
                new MyCartViewModel
                {
                    TotalPrice = 0m,
                    MyProducts = null!
                });

        var coordinator =
            new CheckoutCartCoordinator(
                cartSession.Object,
                products.Object);

        CheckoutCartSnapshot snapshot =
            await coordinator.GetSnapshotAsync(
                user,
                Ct);

        Assert.Equal(0, snapshot.Items);
        Assert.Equal(0m, snapshot.Total);
    }

    [Fact]
    public void MoneyPolicyCoversWalletAndPurchaseCurrencies()
    {
        Assert.Equal(
            12.35m,
            CheckoutOrderMoneyPolicy.ResolveWalletTotal(
                10m,
                1.2345m));

        Assert.Equal(
            10m,
            CheckoutOrderMoneyPolicy.ResolveWalletTotal(
                10m,
                0m));

        CheckoutPurchaseMoney eur =
            CheckoutOrderMoneyPolicy.ResolvePurchase(
                new CheckoutState
                {
                    PaymentMethod =
                        PaymentMethod.CashOnDelivery,
                    Total = 20m
                });

        Assert.Equal(20m, eur.Amount);
        Assert.Equal("EUR", eur.Currency);
        Assert.Equal("EU", eur.Region);

        CheckoutPurchaseMoney usd =
            CheckoutOrderMoneyPolicy.ResolvePurchase(
                new CheckoutState
                {
                    PaymentMethod = PaymentMethod.Card,
                    Total = 10m,
                    Currency = "USD",
                    UsdPerEur = 1.25m
                });

        Assert.Equal(12.50m, usd.Amount);
        Assert.Equal("USD", usd.Currency);
        Assert.Equal("US", usd.Region);

        CheckoutPurchaseMoney usdWithoutRate =
            CheckoutOrderMoneyPolicy.ResolvePurchase(
                new CheckoutState
                {
                    PaymentMethod = PaymentMethod.Card,
                    Total = 10m,
                    Currency = "USD",
                    UsdPerEur = 0m
                });

        Assert.Equal(10m, usdWithoutRate.Amount);
        Assert.Equal("USD", usdWithoutRate.Currency);
        Assert.Equal("US", usdWithoutRate.Region);
    }

    [Fact]
    public async Task TransactionServiceCoversLoggingExistenceAndDuplicateGuard()
    {
        await using AppDbContext db =
            CreateDb();

        var service =
            new CheckoutOrderTransactionService(
                db);

        var user = new User
        {
            Id = "user-1"
        };

        await service.LogPurchaseAsync(
            user,
            new CheckoutState
            {
                OrderId = "invalid",
                Total = 0m,
                PaymentMethod =
                    PaymentMethod.CashOnDelivery
            },
            TransactionStatus.Pending,
            Ct);

        Assert.Empty(db.Transactions);

        var state = new CheckoutState
        {
            OrderId = "cash-order",
            Total = 19.99m,
            PaymentMethod =
                PaymentMethod.CashOnDelivery
        };

        await service.LogPurchaseAsync(
            user,
            state,
            TransactionStatus.Pending,
            Ct);

        Transaction transaction =
            Assert.Single(db.Transactions);

        Assert.Equal(
            "cash-order",
            transaction.ExternalId);
        Assert.Equal(
            19.99m,
            transaction.Amount);
        Assert.Equal(
            "EUR",
            transaction.Currency);
        Assert.Equal(
            "EU",
            transaction.Region);
        Assert.Equal(
            TransactionStatus.Pending,
            transaction.Status);
        Assert.Equal(
            TransactionType.Purchase,
            transaction.Type);
        Assert.Contains(
            "CashOnDelivery",
            transaction.PurchaseTitle);

        Assert.True(
            await service.ExistsAsync(
                user.Id,
                "cash-order",
                Ct));

        await service.LogPurchaseAsync(
            user,
            state,
            TransactionStatus.Complete,
            Ct);

        Assert.Single(db.Transactions);
    }

    [Fact]
    public async Task TransactionServiceCoversUsdCardAndWalletMetadata()
    {
        await using AppDbContext db =
            CreateDb();

        var service =
            new CheckoutOrderTransactionService(
                db);

        var user = new User
        {
            Id = "user-2"
        };

        await service.LogPurchaseAsync(
            user,
            new CheckoutState
            {
                OrderId = "card-order",
                Total = 10m,
                PaymentMethod =
                    PaymentMethod.Card,
                Currency = "USD",
                UsdPerEur = 1.234m
            },
            TransactionStatus.Complete,
            Ct);

        Transaction purchase =
            Assert.Single(db.Transactions);

        Assert.Equal(
            12.34m,
            purchase.Amount);
        Assert.Equal(
            "USD",
            purchase.Currency);
        Assert.Equal(
            "US",
            purchase.Region);

        var walletCharge =
            new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = -5m,
                Currency = "USD",
                Region = "US",
                Status =
                    TransactionStatus.Complete,
                Type =
                    TransactionType.WalletCharge
            };

        db.Transactions.Add(
            walletCharge);

        await db.SaveChangesAsync(Ct);

        await service.AttachOrderMetadataAsync(
            walletCharge.Id,
            "wallet-order",
            Ct);

        Assert.Equal(
            "wallet-order",
            walletCharge.ExternalId);
        Assert.Equal(
            "EU",
            walletCharge.Region);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.AttachOrderMetadataAsync(
                    Guid.NewGuid(),
                    "missing",
                    Ct));
    }

    [Fact]
    public async Task WalletReviewSkipsNonBalancePayments()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object);

        var state =
            new CheckoutState
            {
                PaymentMethod =
                    PaymentMethod.Card,
                UsdPerEur = 9m
            };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                15m);

        Assert.Null(
            review.WalletBalance);
        Assert.Null(
            review.PaymentError);
        Assert.Equal(
            9m,
            state.UsdPerEur);

        wallet.Verify(
            item =>
                item.GetBalanceAsync(
                    It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WalletReviewWithoutRateProviderUsesOrderTotal()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        wallet.Setup(service =>
                service.GetBalanceAsync(
                    "user"))
            .ReturnsAsync(50m);

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object);

        var state =
            new CheckoutState
            {
                PaymentMethod =
                    PaymentMethod.Balance
            };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                20m);

        Assert.Equal(
            50m,
            review.WalletBalance);
        Assert.Null(
            review.PaymentError);
        Assert.Equal(
            0m,
            state.UsdPerEur);
    }

    [Fact]
    public async Task WalletReviewUsesRateAndDetectsInsufficientBalance()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();
        var rates =
            new Mock<IEuroUsdRateProvider>();

        wallet.Setup(service =>
                service.GetBalanceAsync(
                    "user"))
            .ReturnsAsync(10m);

        rates.Setup(service =>
                service.GetUsdPerEurAsync(
                    CancellationToken.None))
            .ReturnsAsync(1.25m);

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object,
                rates.Object);

        var state =
            new CheckoutState
            {
                PaymentMethod =
                    PaymentMethod.Balance
            };

        CheckoutPaymentReview review =
            await service.ReviewAsync(
                new User { Id = "user" },
                state,
                20m);

        Assert.Equal(
            1.25m,
            state.UsdPerEur);
        Assert.Equal(
            "Insufficient wallet balance.",
            review.PaymentError);
    }

    [Fact]
    public async Task WalletReviewMapsRateExceptionAndInvalidRate()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();
        var rates =
            new Mock<IEuroUsdRateProvider>();

        wallet.Setup(service =>
                service.GetBalanceAsync(
                    "user"))
            .ReturnsAsync(100m);

        rates.SetupSequence(service =>
                service.GetUsdPerEurAsync(
                    CancellationToken.None))
            .ThrowsAsync(
                new HttpRequestException())
            .ReturnsAsync(0m);

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object,
                rates.Object);

        CheckoutPaymentReview first =
            await service.ReviewAsync(
                new User { Id = "user" },
                new CheckoutState
                {
                    PaymentMethod =
                        PaymentMethod.Balance
                },
                10m);

        Assert.Contains(
            "exchange rate",
            first.PaymentError!,
            StringComparison.OrdinalIgnoreCase);

        CheckoutPaymentReview second =
            await service.ReviewAsync(
                new User { Id = "user" },
                new CheckoutState
                {
                    PaymentMethod =
                        PaymentMethod.Balance
                },
                10m);

        Assert.Contains(
            "exchange rate",
            second.PaymentError!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WalletChargeRequiresRateWhenProviderExists()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();
        var rates =
            new Mock<IEuroUsdRateProvider>();

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object,
                rates.Object);

        CheckoutOrderPlacementResult result =
            await service.ChargeAsync(
                new User { Id = "user" },
                new CheckoutState
                {
                    Total = 10m,
                    UsdPerEur = 0m
                },
                "order",
                Ct);

        Assert.False(
            result.Success);
        Assert.Contains(
            "exchange rate",
            result.ErrorMessage!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WalletChargeMapsFailureAndAttachesSuccessfulCharge()
    {
        var wallet = new Mock<IWalletService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();
        var user = new User { Id = "user" };

        wallet.SetupSequence(service =>
                service.ChargeAsync(
                    user.Id,
                    12m,
                    "Order order (Wallet)"))
            .ThrowsAsync(
                new InvalidOperationException())
            .ReturnsAsync(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"));

        var service =
            new CheckoutWalletPaymentService(
                wallet.Object,
                transactions.Object);

        var state =
            new CheckoutState
            {
                Total = 10m,
                UsdPerEur = 1.2m
            };

        CheckoutOrderPlacementResult failed =
            await service.ChargeAsync(
                user,
                state,
                "order",
                Ct);

        Assert.False(
            failed.Success);
        Assert.Equal(
            "Insufficient wallet balance.",
            failed.ErrorMessage);

        CheckoutOrderPlacementResult success =
            await service.ChargeAsync(
                user,
                state,
                "order",
                Ct);

        Assert.True(
            success.Success);

        transactions.Verify(service =>
            service.AttachOrderMetadataAsync(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
                "order",
                Ct),
            Times.Once);
    }

    [Fact]
    public async Task FacadeDelegatesQueriesAndCompatibilityConstructorBuildsDefaults()
    {
        await using AppDbContext db =
            CreateDb();

        Assert.NotNull(
            new CheckoutOrderService(
                Mock.Of<ICartSessionService>(),
                Mock.Of<IProductService>(),
                Mock.Of<IWalletService>(),
                db));

        var cart =
            new Mock<ICheckoutCartCoordinator>();
        var walletPayments =
            new Mock<ICheckoutWalletPaymentService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        var user =
            new User { Id = "user" };
        var state =
            new CheckoutState();

        cart.Setup(service =>
                service.GetSnapshotAsync(
                    user,
                    Ct))
            .ReturnsAsync(
                new CheckoutCartSnapshot(
                    2,
                    30m));

        walletPayments.Setup(service =>
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

        var service =
            new CheckoutOrderService(
                cart.Object,
                walletPayments.Object,
                transactions.Object);

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
    public async Task CashOnDeliveryLogsNewOrderAndArchivesDuplicates()
    {
        var cart =
            new Mock<ICheckoutCartCoordinator>();
        var walletPayments =
            new Mock<ICheckoutWalletPaymentService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        var user =
            new User { Id = "user" };
        var state =
            new CheckoutState
            {
                OrderId = "order",
                Total = 20m,
                PaymentMethod =
                    PaymentMethod.CashOnDelivery
            };

        transactions.SetupSequence(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var service =
            new CheckoutOrderService(
                cart.Object,
                walletPayments.Object,
                transactions.Object);

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
            service.ArchiveAsync(
                user,
                Ct),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WalletPlacementPropagatesFailureAndArchivesSuccessOrDuplicate()
    {
        var cart =
            new Mock<ICheckoutCartCoordinator>();
        var walletPayments =
            new Mock<ICheckoutWalletPaymentService>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        var user =
            new User { Id = "user" };
        var state =
            new CheckoutState();

        transactions.SetupSequence(service =>
                service.ExistsAsync(
                    user.Id,
                    "order",
                    Ct))
            .ReturnsAsync(false)
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        walletPayments.SetupSequence(service =>
                service.ChargeAsync(
                    user,
                    state,
                    "order",
                    Ct))
            .ReturnsAsync(
                new CheckoutOrderPlacementResult(
                    false,
                    "failed"))
            .ReturnsAsync(
                new CheckoutOrderPlacementResult(
                    true));

        var service =
            new CheckoutOrderService(
                cart.Object,
                walletPayments.Object,
                transactions.Object);

        CheckoutOrderPlacementResult failed =
            await service.PlaceWalletAsync(
                user,
                state,
                "order",
                Ct);

        Assert.False(
            failed.Success);
        Assert.Equal(
            "failed",
            failed.ErrorMessage);

        CheckoutOrderPlacementResult success =
            await service.PlaceWalletAsync(
                user,
                state,
                "order",
                Ct);

        CheckoutOrderPlacementResult duplicate =
            await service.PlaceWalletAsync(
                user,
                state,
                "order",
                Ct);

        Assert.True(success.Success);
        Assert.True(duplicate.Success);

        walletPayments.Verify(service =>
            service.ChargeAsync(
                user,
                state,
                "order",
                Ct),
            Times.Exactly(2));

        cart.Verify(service =>
            service.ArchiveAsync(
                user,
                Ct),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CardCompletionAndArchiveDelegateToCollaborators()
    {
        var cart =
            new Mock<ICheckoutCartCoordinator>();
        var transactions =
            new Mock<ICheckoutOrderTransactionService>();

        var user =
            new User { Id = "user" };

        var state =
            new CheckoutState
            {
                OrderId = "card-order",
                Total = 50m,
                PaymentMethod =
                    PaymentMethod.Card
            };

        var service =
            new CheckoutOrderService(
                cart.Object,
                Mock.Of<
                    ICheckoutWalletPaymentService>(),
                transactions.Object);

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
            service.ArchiveAsync(
                user,
                Ct),
            Times.Exactly(2));
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
