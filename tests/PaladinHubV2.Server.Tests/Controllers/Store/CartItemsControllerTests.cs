using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

public sealed class CartItemsControllerTests
{
    [Fact]
    public async Task AddItem_WhenRequestMissing_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddItem(null!, CancellationToken.None));

        Assert.False(ReadBoolean(bad.Value, "ok"));
        Assert.Equal("Cart item data is required.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.AddProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddItem_WhenProductIdMissing_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddItem(
                new AddCartItemRequest { ProductId = "   ", Quantity = 1 },
                CancellationToken.None));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.AddProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task AddItem_WhenQuantityOutsideRange_ReturnsBadRequest(int quantity)
    {
        var (controller, session, _, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddItem(
                new AddCartItemRequest { ProductId = "p-1", Quantity = quantity },
                CancellationToken.None));

        Assert.Equal("Quantity must be between 1 and 100.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.AddProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddItem_WhenSessionRejectsProduct_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController();
        session.Setup(service => service.AddProduct("p-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddItem(
                new AddCartItemRequest { ProductId = "p-1", Quantity = 1 },
                CancellationToken.None));

        Assert.Equal("The product could not be added to the cart.", ReadString(bad.Value, "message"));
        session.Verify(service => service.GetCount(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItem_WhenSuccessful_AddsRequestedQuantityAndReturnsCount()
    {
        var (controller, session, _, _) = CreateController();
        session.Setup(service => service.AddProduct("p-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        session.Setup(service => service.GetCount("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.AddItem(
                new AddCartItemRequest { ProductId = "  p-1  ", Quantity = 3 },
                CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("p-1", ReadString(ok.Value, "productId"));
        Assert.Equal(3, ReadInt(ok.Value, "quantityAdded"));
        Assert.Equal(7, ReadInt(ok.Value, "cartCount"));
        session.Verify(
            service => service.AddProduct("p-1", "user-1", It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task AddProduct_LegacyRouteAddsSingleItem()
    {
        var (controller, session, _, _) = CreateController();
        session.Setup(service => service.AddProduct("p-2", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        session.Setup(service => service.GetCount("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.AddProduct("  p-2  ", CancellationToken.None));

        Assert.Equal(1, ReadInt(ok.Value, "quantityAdded"));
        session.Verify(
            service => service.AddProduct("p-2", "user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveProduct_WhenIdMissing_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.RemoveProduct("  ", CancellationToken.None));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        session.Verify(
            service => service.RemoveProduct(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveProduct_WhenSessionRejectsRemoval_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController();
        session.Setup(service => service.RemoveProduct("p-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.RemoveProduct("p-1", CancellationToken.None));

        Assert.Equal("The product could not be removed.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task RemoveProduct_WhenAuthenticated_ReturnsPersistentCartDelta()
    {
        var user = new User { Id = "user-1" };
        var (controller, session, products, users) = CreateController(user);
        session.Setup(service => service.RemoveProduct("p-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        products.Setup(service => service.GetMyProducts(user))
            .ReturnsAsync(new MyCartViewModel
            {
                TotalPrice = 15m,
                MyProducts = new List<ProductViewModel>
                {
                    new() { Id = "p-2", Name = "Other", Price = 15m, Quantity = 1 }
                }
            });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.RemoveProduct(" p-1 ", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("p-1", ReadString(ok.Value, "productId"));
        Assert.True(ReadBoolean(ok.Value, "removed"));
        Assert.Equal(0, ReadInt(ok.Value, "quantity"));
        Assert.Equal(0m, ReadDecimal(ok.Value, "lineTotal"));
        Assert.Equal(15m, ReadDecimal(ok.Value, "cartTotal"));
        session.Verify(service => service.SyncRedisToPersistent(user, It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once);
    }

    private static (
        CartItemsController Controller,
        Mock<ICartSessionService> Session,
        Mock<IProductService> Products,
        Mock<UserManager<User>> Users) CreateController(User? currentUser = null)
    {
        var session = new Mock<ICartSessionService>();
        var products = new Mock<IProductService>();
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);

        var controller = new CartItemsController(
            users.Object,
            session.Object,
            new CartFlowService(session.Object, products.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
                            "Test"))
                }
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
}
