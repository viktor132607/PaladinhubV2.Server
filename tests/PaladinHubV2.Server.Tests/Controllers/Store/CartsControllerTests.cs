using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CartsControllerTests
{
    [Fact]
    public async Task MyCart_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, products, users, session) = CreateController(null, authenticated: false);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(
            await controller.MyCart(CancellationToken.None));

        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        session.Verify(
            service => service.SyncRedisToPersistent(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        products.Verify(service => service.GetMyProducts(It.IsAny<User>()), Times.Never);
        users.Verify(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once);
    }

    [Fact]
    public async Task MyCart_WhenUserExists_SynchronizesAndReturnsCartModel()
    {
        var user = new User { Id = "user-1" };
        var model = new MyCartViewModel { TotalPrice = 42m };
        var (controller, products, _, session) = CreateController(user, authenticated: true);
        products.Setup(service => service.GetMyProducts(user)).ReturnsAsync(model);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.MyCart(CancellationToken.None));

        Assert.Same(model, ok.Value);
        session.Verify(
            service => service.SyncRedisToPersistent(user, It.IsAny<CancellationToken>()),
            Times.Once);
        products.Verify(service => service.GetMyProducts(user), Times.Once);
    }

    [Fact]
    public async Task Mini_WhenAnonymous_ReturnsEmptyZeroTotalCart()
    {
        var (controller, products, _, _) = CreateController(null, authenticated: false);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Mini());
        MyCartViewModel model = Assert.IsType<MyCartViewModel>(ok.Value);

        Assert.Equal(0m, model.TotalPrice);
        Assert.Empty(model.MyProducts);
        products.Verify(service => service.GetMyProducts(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Mini_WhenAuthenticated_ReturnsPersistentProductCart()
    {
        var user = new User { Id = "user-1" };
        var model = new MyCartViewModel { TotalPrice = 19m };
        var (controller, products, _, _) = CreateController(user, authenticated: true);
        products.Setup(service => service.GetMyProducts(user)).ReturnsAsync(model);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Mini());

        Assert.Same(model, ok.Value);
        products.Verify(service => service.GetMyProducts(user), Times.Once);
    }

    [Fact]
    public async Task CountJson_WhenAuthenticated_UsesUserIdentifierAsOwnerKey()
    {
        var (controller, _, _, session) = CreateController(null, authenticated: true);
        session.Setup(service => service.GetCount("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.CountJson(CancellationToken.None));

        Assert.Equal(6, Assert.IsType<int>(ok.Value));
        session.Verify(
            service => service.GetCount("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CountJson_WhenAnonymous_UsesSessionIdentifierAsOwnerKey()
    {
        var (controller, _, _, session) = CreateController(null, authenticated: false);
        session.Setup(service => service.GetCount(
                "anon:unit-test-session",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.CountJson(CancellationToken.None));

        Assert.Equal(3, Assert.IsType<int>(ok.Value));
        session.Verify(
            service => service.GetCount("anon:unit-test-session", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (
        CartsController Controller,
        Mock<IProductService> Products,
        Mock<UserManager<User>> Users,
        Mock<ICartSessionService> Session) CreateController(
            User? currentUser,
            bool authenticated)
    {
        var products = new Mock<IProductService>();
        var session = new Mock<ICartSessionService>();
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

        var controller = new CartsController(
            products.Object,
            users.Object,
            session.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return (controller, products, users, session);
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

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

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
