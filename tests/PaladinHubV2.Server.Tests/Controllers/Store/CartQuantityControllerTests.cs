using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CartQuantityControllerTests
{
    [Fact]
    public async Task Increase_WhenIdMissing_ReturnsBadRequestWithoutCallingSession()
    {
        var (controller, session, _, _) = CreateController(null, authenticated: false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Increase("   ", CancellationToken.None));

        Assert.False(ReadBoolean(bad.Value, "ok"));
        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.IncreaseProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Increase_WhenSessionRejectsUpdate_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController(null, authenticated: false);
        session.Setup(service => service.IncreaseProduct(
                "p-1",
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Increase(" p-1 ", CancellationToken.None));

        Assert.Equal(
            "The product quantity could not be increased.",
            ReadString(bad.Value, "message"));
        session.Verify(
            service => service.GetCount(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Increase_WhenAnonymous_ReturnsAnonymousCartDelta()
    {
        var (controller, session, _, users) = CreateController(null, authenticated: false);
        session.Setup(service => service.IncreaseProduct(
                "p-1",
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        session.Setup(service => service.GetCount(
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Increase(" p-1 ", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("p-1", ReadString(ok.Value, "productId"));
        Assert.False(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(4, ReadInt(ok.Value, "cartCount"));
        users.Verify(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once);
    }

    [Fact]
    public async Task Increase_WhenAuthenticated_ReturnsPersistentCartDelta()
    {
        var user = new User { Id = "user-1" };
        var (controller, session, products, _) = CreateController(user, authenticated: true);
        session.Setup(service => service.IncreaseProduct(
                "p-1",
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(new MyCartViewModel
            {
                TotalPrice = 36m,
                MyProducts = new List<ProductViewModel>
                {
                    new() { Id = "p-1", Name = "Product", Price = 12m, Quantity = 3 }
                }
            });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Increase("p-1", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.False(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(3, ReadInt(ok.Value, "quantity"));
        Assert.Equal(12m, ReadDecimal(ok.Value, "unitPrice"));
        Assert.Equal(36m, ReadDecimal(ok.Value, "lineTotal"));
        Assert.Equal(36m, ReadDecimal(ok.Value, "cartTotal"));
        session.Verify(
            service => service.SyncRedisToPersistent(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Decrease_WhenIdMissing_ReturnsBadRequestWithoutCallingSession()
    {
        var (controller, session, _, _) = CreateController(null, authenticated: false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Decrease("", CancellationToken.None));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.DecreaseProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Decrease_WhenSessionRejectsUpdate_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController(null, authenticated: false);
        session.Setup(service => service.DecreaseProduct(
                "p-1",
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Decrease("p-1", CancellationToken.None));

        Assert.Equal(
            "The product quantity could not be decreased.",
            ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task Decrease_WhenAuthenticatedAndItemRemains_ReturnsCurrentQuantity()
    {
        var user = new User { Id = "user-1" };
        var (controller, session, products, _) = CreateController(user, authenticated: true);
        session.Setup(service => service.DecreaseProduct(
                "p-1",
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(new MyCartViewModel
            {
                TotalPrice = 20m,
                MyProducts = new List<ProductViewModel>
                {
                    new() { Id = "p-1", Name = "Product", Price = 10m, Quantity = 2 }
                }
            });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Decrease("p-1", CancellationToken.None));

        Assert.False(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(2, ReadInt(ok.Value, "quantity"));
        Assert.Equal(20m, ReadDecimal(ok.Value, "lineTotal"));
        Assert.Equal(20m, ReadDecimal(ok.Value, "cartTotal"));
    }

    [Fact]
    public async Task Decrease_WhenAuthenticatedAndItemDisappears_ReturnsRemovedDelta()
    {
        var user = new User { Id = "user-1" };
        var (controller, session, products, _) = CreateController(user, authenticated: true);
        session.Setup(service => service.DecreaseProduct(
                "p-1",
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(new MyCartViewModel { TotalPrice = 0m });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Decrease(" p-1 ", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(0, ReadInt(ok.Value, "quantity"));
        Assert.Equal(0m, ReadDecimal(ok.Value, "unitPrice"));
        Assert.Equal(0m, ReadDecimal(ok.Value, "lineTotal"));
        Assert.Equal(0m, ReadDecimal(ok.Value, "cartTotal"));
    }

    [Fact]
    public async Task Decrease_WhenAnonymous_ReturnsCountAndDefaultsRemovedToFalse()
    {
        var (controller, session, _, _) = CreateController(null, authenticated: false);
        session.Setup(service => service.DecreaseProduct(
                "p-1",
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        session.Setup(service => service.GetCount(
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Decrease("p-1", CancellationToken.None));

        Assert.False(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(2, ReadInt(ok.Value, "cartCount"));
    }

    private static (
        CartQuantityController Controller,
        Mock<ICartSessionService> Session,
        Mock<IProductService> Products,
        Mock<UserManager<User>> Users) CreateController(
            User? currentUser,
            bool authenticated)
    {
        var session = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        Mock<UserManager<User>> users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);

        var testSession = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(testSession));
        httpContext.User = authenticated
            ? new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
                    "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var controller = new CartQuantityController(
            users.Object,
            session.Object,
            new CartFlowService(session.Object, products.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return (controller, session, products, users);
    }

    private static Mock<UserManager<User>> CreateUserManager() => new(
        Mock.Of<IUserStore<User>>(),
        Options.Create(new IdentityOptions()),
        Mock.Of<IPasswordHasher<User>>(),
        Array.Empty<IUserValidator<User>>(),
        Array.Empty<IPasswordValidator<User>>(),
        Mock.Of<ILookupNormalizer>(),
        new IdentityErrorDescriber(),
        Mock.Of<IServiceProvider>(),
        NullLogger<UserManager<User>>.Instance);

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

    private static int ReadInt(object? value, string propertyName) =>
        (int)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0);

    private static decimal ReadDecimal(object? value, string propertyName) =>
        (decimal)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0m);

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id => "unit-test-session";
        public IEnumerable<string> Keys => values.Keys;

        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
