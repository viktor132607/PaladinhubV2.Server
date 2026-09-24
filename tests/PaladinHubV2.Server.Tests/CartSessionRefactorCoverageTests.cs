using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.Tests;

public sealed class CartSessionRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task FacadeRoutesMutationsCountsAndLifecycle()
    {
        var anonymous =
            new Mock<IAnonymousCartSessionService>();
        var persistent =
            new Mock<IPersistentCartSessionService>();
        var lifecycle =
            new Mock<ICartSessionLifecycleService>();
        var policy = new CartSessionRequestPolicy();

        Guid productId = Guid.NewGuid();
        string canonicalProductId = productId.ToString();

        anonymous.Setup(x => x.AddProductAsync(
                "anon:abc",
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        anonymous.Setup(x => x.IncreaseProductAsync(
                "anon:abc",
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        anonymous.Setup(x => x.DecreaseProductAsync(
                "anon:abc",
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        anonymous.Setup(x => x.RemoveProductAsync(
                "anon:abc",
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        anonymous.Setup(x => x.GetCountAsync(
                "anon:abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        persistent.Setup(x => x.AddProductAsync(
                "user-1",
                canonicalProductId))
            .ReturnsAsync(true);
        persistent.Setup(x => x.IncreaseProductAsync(
                "user-1",
                canonicalProductId))
            .ReturnsAsync(true);
        persistent.Setup(x => x.DecreaseProductAsync(
                "user-1",
                canonicalProductId))
            .ReturnsAsync(true);
        persistent.Setup(x => x.RemoveProductAsync(
                "user-1",
                canonicalProductId))
            .ReturnsAsync(true);
        persistent.Setup(x => x.GetCountAsync(
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var service = new CartSessionService(
            policy,
            anonymous.Object,
            persistent.Object,
            lifecycle.Object);

        Assert.True(await service.AddProduct(
            $" {productId} ",
            " anon:abc ",
            Ct));
        Assert.True(await service.IncreaseProduct(
            canonicalProductId,
            "anon:abc",
            Ct));
        Assert.True(await service.DecreaseProduct(
            canonicalProductId,
            "anon:abc",
            Ct));
        Assert.True(await service.RemoveProduct(
            canonicalProductId,
            "anon:abc",
            Ct));

        Assert.True(await service.AddProduct(
            canonicalProductId,
            " user-1 ",
            Ct));
        Assert.True(await service.IncreaseProduct(
            canonicalProductId,
            "user-1",
            Ct));
        Assert.True(await service.DecreaseProduct(
            canonicalProductId,
            "user-1",
            Ct));
        Assert.True(await service.RemoveProduct(
            canonicalProductId,
            "user-1",
            Ct));

        Assert.False(await service.AddProduct(
            canonicalProductId,
            " ",
            Ct));
        Assert.False(await service.AddProduct(
            "not-a-guid",
            "user-1",
            Ct));

        Assert.Equal(
            0,
            await service.GetCount(" ", Ct));
        Assert.Equal(
            3,
            await service.GetCount("anon:abc", Ct));
        Assert.Equal(
            5,
            await service.GetCount("user-1", Ct));

        var user = new User { Id = "user-1" };
        await service.ArchiveAndClear(user, Ct);
        await service.CleanAndClear(user, Ct);
        await service.SyncRedisToPersistent(user, Ct);

        lifecycle.Verify(x => x.ArchiveAndClearAsync(
            user,
            It.IsAny<CancellationToken>()),
            Times.Once);
        lifecycle.Verify(x => x.CleanAndClearAsync(
            user,
            It.IsAny<CancellationToken>()),
            Times.Once);
        lifecycle.Verify(x => x.SyncStoreToPersistentAsync(
            user,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RequestPolicyNormalizesAndClassifiesOwnersAndProducts()
    {
        var policy = new CartSessionRequestPolicy();

        Assert.False(policy.TryNormalizeOwner(
            null,
            out string emptyOwner));
        Assert.Equal(string.Empty, emptyOwner);

        Assert.True(policy.TryNormalizeOwner(
            " User-1 ",
            out string owner));
        Assert.Equal("User-1", owner);

        Assert.True(policy.IsAnonymousOwner("ANON:session"));
        Assert.False(policy.IsAnonymousOwner("user-1"));

        Assert.False(policy.TryNormalizeProduct(
            "bad",
            out Guid invalidGuid,
            out string invalidId));
        Assert.Equal(Guid.Empty, invalidGuid);
        Assert.Equal(string.Empty, invalidId);

        Guid id = Guid.NewGuid();
        Assert.True(policy.TryNormalizeProduct(
            $" {id} ",
            out Guid parsed,
            out string normalized));
        Assert.Equal(id, parsed);
        Assert.Equal(id.ToString(), normalized);
    }

    [Fact]
    public async Task AnonymousServiceCoversAllMutationBranchesAndCount()
    {
        await using AppDbContext db = CreateDb();
        Guid productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId.ToString(),
            Name = "Product",
            Price = 10m
        });
        await db.SaveChangesAsync(Ct);

        var store = new Mock<ICartStore>();
        var service = new AnonymousCartSessionService(
            store.Object,
            db);

        Guid missingProduct = Guid.NewGuid();
        Assert.False(await service.AddProductAsync(
            "anon:a",
            missingProduct,
            Ct));

        store.SetupSequence(x => x.GetAsync(
                "anon:a",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CartLine>())
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 2
                }]);

        Assert.True(await service.AddProductAsync(
            "anon:a",
            productId,
            Ct));
        Assert.True(await service.AddProductAsync(
            "anon:a",
            productId,
            Ct));

        store.Verify(x => x.AddOrUpdateAsync(
            "anon:a",
            productId,
            1,
            It.IsAny<CancellationToken>()),
            Times.Once);
        store.Verify(x => x.AddOrUpdateAsync(
            "anon:a",
            productId,
            3,
            It.IsAny<CancellationToken>()),
            Times.Once);

        store.Setup(x => x.GetAsync(
                "anon:increase-missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CartLine>());
        Assert.False(await service.IncreaseProductAsync(
            "anon:increase-missing",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:increase-zero",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 0
                }]);
        Assert.False(await service.IncreaseProductAsync(
            "anon:increase-zero",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:increase",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 4
                }]);
        Assert.True(await service.IncreaseProductAsync(
            "anon:increase",
            productId,
            Ct));
        store.Verify(x => x.AddOrUpdateAsync(
            "anon:increase",
            productId,
            5,
            It.IsAny<CancellationToken>()),
            Times.Once);

        store.Setup(x => x.GetAsync(
                "anon:decrease-missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CartLine>());
        Assert.False(await service.DecreaseProductAsync(
            "anon:decrease-missing",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:decrease-zero",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 0
                }]);
        Assert.False(await service.DecreaseProductAsync(
            "anon:decrease-zero",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:decrease",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 1
                }]);
        Assert.True(await service.DecreaseProductAsync(
            "anon:decrease",
            productId,
            Ct));
        store.Verify(x => x.AddOrUpdateAsync(
            "anon:decrease",
            productId,
            0,
            It.IsAny<CancellationToken>()),
            Times.Once);

        store.Setup(x => x.GetAsync(
                "anon:remove-missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CartLine>());
        Assert.False(await service.RemoveProductAsync(
            "anon:remove-missing",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:remove",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = productId,
                    Quantity = 3
                }]);
        Assert.True(await service.RemoveProductAsync(
            "anon:remove",
            productId,
            Ct));

        store.Setup(x => x.GetAsync(
                "anon:count",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new CartLine
                    {
                        ProductId = productId,
                        Quantity = 3
                    },
                    new CartLine
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = -2
                    }
                ]);

        Assert.Equal(
            3,
            await service.GetCountAsync("anon:count", Ct));
    }

    [Fact]
    public async Task PersistentServiceDelegatesMutationsAndCountsActiveCart()
    {
        await using AppDbContext db = CreateDb();

        var cartService = new Mock<ICartService>();
        var service = new PersistentCartSessionService(
            cartService.Object,
            db);

        cartService.Setup(x => x.AddProduct("p", "user-1"))
            .ReturnsAsync(true);
        cartService.Setup(x => x.IncreaseProduct("p", "user-1"))
            .ReturnsAsync(true);
        cartService.Setup(x => x.DecreaseProduct("p", "user-1"))
            .ReturnsAsync(true);
        cartService.Setup(x => x.RemoveProduct("p", "user-1"))
            .ReturnsAsync(true);

        Assert.True(await service.AddProductAsync("user-1", "p"));
        Assert.True(await service.IncreaseProductAsync("user-1", "p"));
        Assert.True(await service.DecreaseProductAsync("user-1", "p"));
        Assert.True(await service.RemoveProductAsync("user-1", "p"));

        Assert.Equal(
            0,
            await service.GetCountAsync("user-1", Ct));

        Guid activeCartId = Guid.NewGuid();
        Guid archivedCartId = Guid.NewGuid();

        db.Carts.AddRange(
            new Cart
            {
                Id = activeCartId,
                UserId = "user-1",
                Status = "Pending",
                IsArchived = false
            },
            new Cart
            {
                Id = archivedCartId,
                UserId = "user-1",
                Status = "Pending",
                IsArchived = true
            });

        db.CartProducts.AddRange(
            new CartProduct
            {
                CartId = activeCartId,
                ProductId = "product-a",
                Quantity = 2
            },
            new CartProduct
            {
                CartId = activeCartId,
                ProductId = "product-b",
                Quantity = 3
            },
            new CartProduct
            {
                CartId = archivedCartId,
                ProductId = "product-c",
                Quantity = 99
            });

        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            5,
            await service.GetCountAsync("user-1", Ct));
    }

    [Fact]
    public async Task LifecycleArchivesCleansAndSynchronizesStore()
    {
        var cartService = new Mock<ICartService>();
        var store = new Mock<ICartStore>();
        var service = new CartSessionLifecycleService(
            cartService.Object,
            store.Object);

        var user = new User { Id = "user-1" };

        cartService.Setup(x => x.ArchiveCart(user))
            .Returns(Task.CompletedTask);
        cartService.Setup(x => x.CleanCart(user))
            .Returns(Task.CompletedTask);
        cartService.Setup(x => x.AddProduct(
                It.IsAny<string>(),
                user.Id))
            .ReturnsAsync(true);
        store.Setup(x => x.ClearAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.ArchiveAndClearAsync(user, Ct);
        await service.CleanAndClearAsync(user, Ct);

        Assert.Throws<ArgumentNullException>(
            () => service.ArchiveAndClearAsync(null!, Ct)
                .GetAwaiter()
                .GetResult());
        Assert.Throws<ArgumentNullException>(
            () => service.CleanAndClearAsync(null!, Ct)
                .GetAwaiter()
                .GetResult());
        Assert.Throws<ArgumentNullException>(
            () => service.SyncStoreToPersistentAsync(null!, Ct)
                .GetAwaiter()
                .GetResult());

        store.SetupSequence(x => x.GetAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CartLine>())
            .ReturnsAsync(
                [
                    new CartLine
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 0
                    },
                    new CartLine
                    {
                        ProductId = Guid.Parse(
                            "11111111-1111-1111-1111-111111111111"),
                        Quantity = 2
                    }
                ]);

        await service.SyncStoreToPersistentAsync(user, Ct);
        await service.SyncStoreToPersistentAsync(user, Ct);

        cartService.Verify(
            x => x.AddProduct(
                "11111111-1111-1111-1111-111111111111",
                user.Id),
            Times.Exactly(2));
    }

    [Fact]
    public async Task LifecycleHonorsCancellationDuringSynchronization()
    {
        var cartService = new Mock<ICartService>();
        var store = new Mock<ICartStore>();
        var service = new CartSessionLifecycleService(
            cartService.Object,
            store.Object);
        var user = new User { Id = "user-1" };
        var cancelled = new CancellationToken(canceled: true);

        store.Setup(x => x.GetAsync(
                user.Id,
                cancelled))
            .ReturnsAsync(
                [new CartLine
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1
                }]);
        cartService.Setup(x => x.CleanCart(user))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.SyncStoreToPersistentAsync(
                user,
                cancelled));
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "cart-session-" + Guid.NewGuid())
                .Options;

        return new AppDbContext(options);
    }
}
