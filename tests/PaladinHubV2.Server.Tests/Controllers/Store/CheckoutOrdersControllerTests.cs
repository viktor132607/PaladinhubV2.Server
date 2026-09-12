using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CheckoutOrdersControllerTests
{
    [Fact]
    public async Task PlaceOrder_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, orders, _) = CreateController(null, new CheckoutState());

        IActionResult result = await controller.PlaceOrder(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaceOrder_WhenCheckoutStateIncomplete_ReturnsBadRequest()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, orders, _) = CreateController(user, new CheckoutState());

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.Contains("Shipping details", ReadString(bad.Value, "message"), StringComparison.OrdinalIgnoreCase);
        orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0, 15.0)]
    [InlineData(2, 0.0)]
    public async Task PlaceOrder_WhenCartEmpty_ReturnsBadRequest(int items, double total)
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Card);
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(items, (decimal)total));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.Equal("Your cart is empty.", ReadString(bad.Value, "message"));
        session.Verify(service => service.SaveState(It.IsAny<CheckoutState>()), Times.Never);
    }

    [Fact]
    public async Task PlaceOrder_Card_GeneratesOrderIdAndReturnsCardRedirect()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Card);
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 50m));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.False(string.IsNullOrWhiteSpace(state.OrderId));
        Assert.Equal(50m, state.Total);
        Assert.Equal(state.OrderId, ReadString(ok.Value, "orderId"));
        Assert.Equal("/Checkout/Card", ReadString(ok.Value, "redirect"));
        session.Verify(service => service.SaveState(state), Times.Once);
        session.Verify(service => service.Clear(), Times.Never);
        orders.Verify(
            service => service.PlaceCashOnDeliveryAsync(It.IsAny<User>(), It.IsAny<CheckoutState>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        orders.Verify(
            service => service.PlaceWalletAsync(It.IsAny<User>(), It.IsAny<CheckoutState>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaceOrder_CashOnDelivery_PreservesOrderIdPlacesAndClears()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.CashOnDelivery);
        state.OrderId = "existing-order";
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(1, 25m));
        orders.Setup(service => service.PlaceCashOnDeliveryAsync(
                user,
                state,
                "existing-order",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutOrderPlacementResult(true));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("existing-order", ReadString(ok.Value, "orderId"));
        Assert.Equal("/Checkout/Registered", ReadString(ok.Value, "redirect"));
        orders.Verify(service => service.PlaceCashOnDeliveryAsync(
            user,
            state,
            "existing-order",
            It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(service => service.Clear(), Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_CashOnDelivery_PropagatesServiceSuccessFlag()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.CashOnDelivery);
        state.OrderId = "order-2";
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(1, 10m));
        orders.Setup(service => service.PlaceCashOnDeliveryAsync(
                user,
                state,
                "order-2",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutOrderPlacementResult(false, "Failed"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.False(ReadBoolean(ok.Value, "ok"));
        session.Verify(service => service.Clear(), Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_Balance_WhenChargeFails_ReturnsPaymentErrorAndDoesNotClear()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Balance);
        state.OrderId = "wallet-order";
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 70m));
        orders.Setup(service => service.PlaceWalletAsync(
                user,
                state,
                "wallet-order",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutOrderPlacementResult(false, "Wallet too low."));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.Equal("Wallet too low.", ReadString(bad.Value, "message"));
        Assert.Equal("Wallet too low.", ReadString(bad.Value, "paymentError"));
        Assert.Equal("/Checkout/Review", ReadString(bad.Value, "redirect"));
        session.Verify(service => service.Clear(), Times.Never);
    }

    [Fact]
    public async Task PlaceOrder_Balance_WhenServiceHasNoMessage_UsesDefaultError()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Balance);
        state.OrderId = "wallet-order";
        var (controller, _, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 70m));
        orders.Setup(service => service.PlaceWalletAsync(
                user,
                state,
                "wallet-order",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutOrderPlacementResult(false));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.Equal("Insufficient wallet balance.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task PlaceOrder_Balance_WhenSuccessful_ClearsAndReturnsSuccess()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Balance);
        state.OrderId = "wallet-order";
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 70m));
        orders.Setup(service => service.PlaceWalletAsync(
                user,
                state,
                "wallet-order",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutOrderPlacementResult(true));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("/Checkout/Success", ReadString(ok.Value, "redirect"));
        session.Verify(service => service.Clear(), Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_WhenPaymentMethodValueInvalid_ReturnsBadRequest()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState((PaymentMethod)999);
        state.OrderId = "order-invalid";
        var (controller, session, orders, _) = CreateController(user, state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(1, 12m));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.PlaceOrder(CancellationToken.None));

        Assert.Equal("Invalid payment method.", ReadString(bad.Value, "message"));
        session.Verify(service => service.SaveState(state), Times.Once);
        session.Verify(service => service.Clear(), Times.Never);
    }

    private static (
        CheckoutOrdersController Controller,
        Mock<ICheckoutSessionService> Session,
        Mock<ICheckoutOrderService> Orders,
        Mock<UserManager<User>> Users) CreateController(
        User? currentUser,
        CheckoutState state)
    {
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);

        var session = new Mock<ICheckoutSessionService>();
        session.Setup(service => service.GetState()).Returns(state);
        var orders = new Mock<ICheckoutOrderService>();

        var controller = new CheckoutOrdersController(
            users.Object,
            session.Object,
            orders.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return (controller, session, orders, users);
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

    private static CheckoutState CompleteState(PaymentMethod method) => new()
    {
        Shipping = new ShippingInfoVM
        {
            FullName = "Test User",
            Address = "1 Test Street",
            City = "Varna",
            PostalCode = "9000",
            Country = "Bulgaria",
            Phone = "+359888000000"
        },
        PaymentMethod = method
    };

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
