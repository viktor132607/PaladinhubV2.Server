using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using Xunit;

namespace PaladinHubV2.Server.Tests;

public sealed class CartRefactorCoverageTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 25, 1, 30, 45, TimeSpan.Zero);

    [Fact]
    public async Task ActiveCartServiceCoversMutationsAndCleaning()
    {
        await using AppDbContext db = CreateDb();
        var service = new CartActiveService(
            db,
            new FixedTimeProvider(FixedNow));

        string productId = Guid.NewGuid().ToString();
        string otherProductId = Guid.NewGuid().ToString();

        Assert.False(await service.AddProduct(
            productId,
            "missing-user"));

        db.Products.AddRange(
            new Product
            {
                Id = productId,
                Name = "Helm",
                Price = 20m
            },
            new Product
            {
                Id = otherProductId,
                Name = "Shield",
                Price = 30m
            });
        await db.SaveChangesAsync();

        Assert.False(await service.AddProduct(
            productId,
            "missing-user"));

        var user = new User
        {
            Id = "user-1",
            UserName = "Viktor"
        };
        var userWithoutCart = new User
        {
            Id = "user-2",
            UserName = "Other"
        };
        db.Users.AddRange(user, userWithoutCart);
        await db.SaveChangesAsync();

        Assert.True(await service.AddProduct(
            productId,
            user.Id));

        Cart activeCart = await db.Carts.SingleAsync(
            cart => cart.UserId == user.Id &&
                    !cart.IsArchived);
        Assert.Equal(
            FixedNow.UtcDateTime,
            activeCart.UpdatedOn);

        CartProduct line = await db.CartProducts.SingleAsync(
            item => item.CartId == activeCart.Id &&
                    item.ProductId == productId);
        Assert.Equal(1, line.Quantity);

        Assert.True(await service.AddProduct(
            productId,
            user.Id));

        line = await db.CartProducts.SingleAsync(
            item => item.CartId == activeCart.Id &&
                    item.ProductId == productId);
        Assert.Equal(2, line.Quantity);

        Assert.False(await service.IncreaseProduct(
            productId,
            "missing-user"));
        Assert.False(await service.IncreaseProduct(
            productId,
            userWithoutCart.Id));
        Assert.False(await service.IncreaseProduct(
            otherProductId,
            user.Id));

        Assert.True(await service.IncreaseProduct(
            productId,
            user.Id));
        Assert.Equal(
            3,
            (await db.CartProducts.SingleAsync(
                item => item.CartId == activeCart.Id &&
                        item.ProductId == productId))
            .Quantity);

        Assert.False(await service.DecreaseProduct(
            otherProductId,
            user.Id));

        Assert.True(await service.DecreaseProduct(
            productId,
            user.Id));
        Assert.Equal(
            2,
            (await db.CartProducts.SingleAsync(
                item => item.CartId == activeCart.Id &&
                        item.ProductId == productId))
            .Quantity);

        Assert.True(await service.DecreaseProduct(
            productId,
            user.Id));
        Assert.Equal(
            1,
            (await db.CartProducts.SingleAsync(
                item => item.CartId == activeCart.Id &&
                        item.ProductId == productId))
            .Quantity);

        Assert.True(await service.DecreaseProduct(
            productId,
            user.Id));
        Assert.False(await db.CartProducts.AnyAsync(
            item => item.CartId == activeCart.Id &&
                    item.ProductId == productId));

        Assert.False(await service.RemoveProduct(
            productId,
            "missing-user"));
        Assert.False(await service.RemoveProduct(
            productId,
            userWithoutCart.Id));
        Assert.False(await service.RemoveProduct(
            productId,
            user.Id));

        Assert.True(await service.AddProduct(
            productId,
            user.Id));
        Assert.True(await service.RemoveProduct(
            productId,
            user.Id));
        Assert.False(await db.CartProducts.AnyAsync(
            item => item.CartId == activeCart.Id));

        await service.CleanCart(null!);
        await service.CleanCart(userWithoutCart);

        await service.CleanCart(user);
        Assert.Equal(
            FixedNow.UtcDateTime,
            (await db.Carts.SingleAsync(
                cart => cart.Id == activeCart.Id))
            .UpdatedOn);

        Assert.True(await service.AddProduct(
            productId,
            user.Id));
        Assert.True(await service.AddProduct(
            otherProductId,
            user.Id));

        await service.CleanCart(user);

        Assert.False(await db.CartProducts.AnyAsync(
            item => item.CartId == activeCart.Id));
    }

    [Fact]
    public async Task ArchiveMutationServiceCoversArchiveAndStatusBranches()
    {
        await using AppDbContext db = CreateDb();
        var service = new CartArchiveMutationService(
            db,
            new FixedTimeProvider(FixedNow));

        var missingUser = new User
        {
            Id = "missing",
            UserName = "Missing"
        };

        await service.ArchiveCart(null!);
        await service.ArchiveCart(missingUser);

        var user = new User
        {
            Id = "user-1",
            UserName = "Viktor"
        };
        var active = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Status = OrderStatusCatalog.Processing,
            UpdatedOn = FixedNow.AddDays(-1).UtcDateTime
        };

        db.Users.Add(user);
        db.Carts.Add(active);
        await db.SaveChangesAsync();

        await service.ArchiveCart(user);

        Cart archived = await db.Carts.SingleAsync(
            cart => cart.Id == active.Id);
        Assert.True(archived.IsArchived);
        Assert.Equal(
            "2026-09-25 01:30:45",
            archived.OrderDate);
        Assert.Equal(
            OrderStatusCatalog.Pending,
            archived.Status);
        Assert.Equal(
            FixedNow.UtcDateTime,
            archived.UpdatedOn);

        Cart replacement = await db.Carts.SingleAsync(
            cart => cart.UserId == user.Id &&
                    !cart.IsArchived);
        Assert.NotEqual(active.Id, replacement.Id);
        Assert.Equal(
            FixedNow.UtcDateTime,
            replacement.UpdatedOn);

        Assert.False(await service.UpdateOrderStatus(
            archived.Id,
            "invalid"));
        Assert.False(await service.UpdateOrderStatus(
            Guid.NewGuid(),
            OrderStatusCatalog.Completed));

        Assert.True(await service.UpdateOrderStatus(
            archived.Id,
            " completed "));

        archived = await db.Carts.SingleAsync(
            cart => cart.Id == active.Id);
        Assert.Equal(
            OrderStatusCatalog.Completed,
            archived.Status);
        Assert.Equal(
            FixedNow.UtcDateTime,
            archived.UpdatedOn);
    }

    [Fact]
    public async Task ArchiveQueryServiceCoversArchiveQueriesAndArchivedOnlyDetails()
    {
        await using AppDbContext db = CreateDb();

        var user = new User
        {
            Id = "user-1",
            UserName = "Viktor"
        };
        string productId = Guid.NewGuid().ToString();
        var product = new Product
        {
            Id = productId,
            Name = "Sword",
            Price = 15m
        };
        var archived = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            IsArchived = true,
            OrderDate = "2026-09-24 20:00:00",
            Status = string.Empty
        };
        var active = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            IsArchived = false,
            Status = OrderStatusCatalog.Pending
        };
        var line = new CartProduct
        {
            CartId = archived.Id,
            Cart = archived,
            ProductId = productId,
            Product = product,
            Quantity = 2
        };

        db.Users.Add(user);
        db.Products.Add(product);
        db.Carts.AddRange(archived, active);
        db.CartProducts.Add(line);
        await db.SaveChangesAsync();

        var service = new CartArchiveQueryService(db);

        ICollection<CartViewModel> legacyArchive =
            await service.GetArchive();
        CartViewModel legacy = Assert.Single(legacyArchive);
        Assert.Equal(archived.Id, legacy.Id);
        Assert.Equal(user.Id, legacy.UserId);
        Assert.Equal("Viktor", legacy.User?.UserName);
        Assert.Equal(
            "2026-09-24 20:00:00",
            legacy.OrderDate);
        Assert.Single(legacy.CartProducts);
        Assert.Single(legacy.Products);

        IReadOnlyCollection<ArchivedOrderSummary> summaries =
            await service.GetArchivedOrders();
        ArchivedOrderSummary summary =
            Assert.Single(summaries);
        Assert.Equal(archived.Id, summary.Id);
        Assert.Equal("Viktor", summary.Username);
        Assert.Equal(
            OrderStatusCatalog.Pending,
            summary.Status);

        Assert.Null(await service.GetArchivedOrder(
            Guid.NewGuid()));

        ArchivedOrderDetails details =
            Assert.IsType<ArchivedOrderDetails>(
                await service.GetArchivedOrder(
                    archived.Id));
        Assert.Equal("Viktor", details.Username);
        Assert.Equal(
            OrderStatusCatalog.Pending,
            details.Status);
        ArchivedOrderItem item =
            Assert.Single(details.Items);
        Assert.Equal(productId, item.Id);
        Assert.Equal("Sword", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(15m, item.Price);
        Assert.Equal(string.Empty, item.ImageUrl);
        Assert.Equal(30m, details.TotalPrice);

        Assert.Null(await service.GetCartById(
            active.Id));
        Assert.Null(await service.GetCartById(
            Guid.NewGuid()));

        MyCartViewModel archivedCart =
            Assert.IsType<MyCartViewModel>(
                await service.GetCartById(
                    archived.Id));
        var archivedProduct =
            Assert.Single(archivedCart.MyProducts);
        Assert.Equal(productId, archivedProduct.Id);
        Assert.Equal("Sword", archivedProduct.Name);
        Assert.Equal(15m, archivedProduct.Price);
        Assert.Equal(2, archivedProduct.Quantity);
        Assert.Equal(archived.Id, archivedProduct.CartId);
        Assert.Equal(30m, archivedCart.TotalPrice);
    }

    [Fact]
    public async Task CartServiceFacadeDelegatesEveryOperation()
    {
        Guid cartId = Guid.NewGuid();
        var user = new User
        {
            Id = "user-1",
            UserName = "Viktor"
        };
        var cartModel = new MyCartViewModel();
        ICollection<CartViewModel> legacyArchive =
            [new CartViewModel { Id = cartId, UserId = user.Id }];
        IReadOnlyCollection<ArchivedOrderSummary> summaries =
            [new ArchivedOrderSummary(
                cartId,
                "Viktor",
                "2026-09-25",
                OrderStatusCatalog.Pending)];
        var details = new ArchivedOrderDetails(
            cartId,
            "Viktor",
            "2026-09-25",
            OrderStatusCatalog.Pending,
            Array.Empty<ArchivedOrderItem>(),
            0m);

        var active = new Mock<ICartActiveService>();
        var queries = new Mock<ICartArchiveQueryService>();
        var mutations = new Mock<ICartArchiveMutationService>();

        active.Setup(service =>
                service.AddProduct("p", user.Id))
            .ReturnsAsync(true);
        active.Setup(service =>
                service.IncreaseProduct("p", user.Id))
            .ReturnsAsync(true);
        active.Setup(service =>
                service.DecreaseProduct("p", user.Id))
            .ReturnsAsync(true);
        active.Setup(service =>
                service.RemoveProduct("p", user.Id))
            .ReturnsAsync(true);
        active.Setup(service =>
                service.CleanCart(user))
            .Returns(Task.CompletedTask);

        queries.Setup(service =>
                service.GetCartById(cartId))
            .ReturnsAsync(cartModel);
        queries.Setup(service =>
                service.GetArchive())
            .ReturnsAsync(legacyArchive);
        queries.Setup(service =>
                service.GetArchivedOrders())
            .ReturnsAsync(summaries);
        queries.Setup(service =>
                service.GetArchivedOrder(cartId))
            .ReturnsAsync(details);

        mutations.Setup(service =>
                service.UpdateOrderStatus(
                    cartId,
                    OrderStatusCatalog.Shipped))
            .ReturnsAsync(true);
        mutations.Setup(service =>
                service.ArchiveCart(user))
            .Returns(Task.CompletedTask);

        var service = new CartService(
            active.Object,
            queries.Object,
            mutations.Object);

        Assert.Same(
            cartModel,
            await service.GetCartById(cartId));
        Assert.Same(
            legacyArchive,
            await service.GetArchive());
        Assert.Same(
            summaries,
            await service.GetArchivedOrders());
        Assert.Same(
            details,
            await service.GetArchivedOrder(cartId));
        Assert.True(await service.UpdateOrderStatus(
            cartId,
            OrderStatusCatalog.Shipped));
        Assert.True(await service.AddProduct(
            "p",
            user.Id));
        Assert.True(await service.IncreaseProduct(
            "p",
            user.Id));
        Assert.True(await service.DecreaseProduct(
            "p",
            user.Id));
        Assert.True(await service.RemoveProduct(
            "p",
            user.Id));

        await service.ArchiveCart(user);
        await service.CleanCart(user);

        mutations.Verify(
            value => value.ArchiveCart(user),
            Times.Once);
        active.Verify(
            value => value.CleanCart(user),
            Times.Once);
    }

    [Fact]
    public void CartModelAllowsHistoryAndKeepsSingleActiveCartIndex()
    {
        using AppDbContext db = CreateDb();

        var cartType = db.Model.FindEntityType(typeof(Cart));
        Assert.NotNull(cartType);

        var userForeignKey = Assert.Single(
            cartType!.GetForeignKeys()
                .Where(key =>
                    key.PrincipalEntityType.ClrType ==
                    typeof(User)));
        Assert.False(userForeignKey.IsUnique);

        var activeIndex = Assert.Single(
            cartType.GetIndexes()
                .Where(index =>
                    index.IsUnique &&
                    index.Properties.Count == 1 &&
                    index.Properties[0].Name ==
                    nameof(Cart.UserId)));

        string? filter = activeIndex.GetFilter();
        Assert.NotNull(filter);
        Assert.Contains(
            "IsArchived",
            filter!,
            StringComparison.Ordinal);
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "cart-refactor-" + Guid.NewGuid())
                .Options;

        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider :
        TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;
    }
}
