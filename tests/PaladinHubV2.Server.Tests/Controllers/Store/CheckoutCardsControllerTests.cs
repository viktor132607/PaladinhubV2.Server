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
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CheckoutCardsControllerTests
{
    [Fact]
    public async Task Card_WhenUserMissing_ReturnsUnauthorizedWithoutReadingCheckoutState()
    {
        var context = CreateContext(null, new CheckoutState());

        IActionResult result = await context.Controller.Card(CancellationToken.None);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        context.Session.Verify(service => service.GetState(), Times.Never);
    }

    [Fact]
    public async Task Card_WhenShippingMissing_ReturnsConflictWithShippingRedirect()
    {
        var context = CreateContext(User(), new CheckoutState());

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal("Shipping details are required.", ReadString(conflict.Value, "message"));
        Assert.Equal("/Checkout/Shipping", ReadString(conflict.Value, "redirect"));
        context.Orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Card_WhenCardNotSelected_ReturnsConflictWithReviewRedirect()
    {
        var state = new CheckoutState
        {
            Shipping = Shipping(),
            PaymentMethod = CheckoutPaymentMethod.Balance
        };
        var context = CreateContext(User(), state);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal("Card payment is not selected.", ReadString(conflict.Value, "message"));
        Assert.Equal("/Checkout/Review", ReadString(conflict.Value, "redirect"));
    }

    [Theory]
    [InlineData(0, 25.0)]
    [InlineData(2, 0.0)]
    public async Task Card_WhenCartIsEmpty_ReturnsBadRequest(int items, double total)
    {
        var user = User();
        var context = CreateContext(user, CardState());
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(items, (decimal)total));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal("Your cart is empty.", ReadString(bad.Value, "message"));
        Assert.Equal("/Cart/MyCart", ReadString(bad.Value, "redirect"));
        context.Payments.VerifyGet(service => service.IsConfigured, Times.Never);
    }

    [Fact]
    public async Task Card_WhenStripeNotConfigured_ReturnsServiceUnavailable()
    {
        var user = User();
        var context = CreateContext(user, CardState());
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 42m));
        context.Payments.SetupGet(service => service.IsConfigured).Returns(false);

        ObjectResult error = Assert.IsType<ObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.Equal("Stripe is not configured.", ReadString(error.Value, "message"));
        context.Session.Verify(service => service.SaveState(It.IsAny<CheckoutState>()), Times.Never);
    }

    [Fact]
    public async Task Card_WhenStripeOmitsClientSecret_ReturnsBadGateway()
    {
        var user = User();
        var state = CardState();
        var context = CreateContext(user, state);
        SetupReadyCart(context, user, 42m);
        context.Payments
            .Setup(service => service.CreateSessionAsync(
                user.Id,
                It.IsAny<string>(),
                42m,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardSessionResult(CheckoutCardPaymentError.MissingClientSecret));

        ObjectResult error = Assert.IsType<ObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal("Stripe did not return a client secret.", ReadString(error.Value, "message"));
        Assert.Equal(42m, state.Total);
        Assert.False(string.IsNullOrWhiteSpace(state.OrderId));
        context.Session.Verify(service => service.SaveState(state), Times.Once);
    }

    [Fact]
    public async Task Card_WhenStripeSessionCreationFails_ReturnsBadGateway()
    {
        var user = User();
        var context = CreateContext(user, CardState());
        SetupReadyCart(context, user, 35m);
        context.Payments
            .Setup(service => service.CreateSessionAsync(
                user.Id,
                It.IsAny<string>(),
                35m,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardSessionResult(CheckoutCardPaymentError.CreateFailed));

        ObjectResult error = Assert.IsType<ObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal("Card payment session could not be created.", ReadString(error.Value, "message"));
    }

    [Fact]
    public async Task Card_WhenReady_ReturnsStripeSessionAndPersistsOrderState()
    {
        var user = User();
        var state = CardState();
        var context = CreateContext(user, state);
        SetupReadyCart(context, user, 57.25m);
        context.Payments
            .Setup(service => service.CreateSessionAsync(
                user.Id,
                It.IsAny<string>(),
                57.25m,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardSessionResult(
                CheckoutCardPaymentError.None,
                "secret-1",
                "pk_test_1",
                "pi_1",
                57.25m,
                "USD"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await context.Controller.Card(CancellationToken.None));

        Assert.Equal("secret-1", ReadString(ok.Value, "clientSecret"));
        Assert.Equal("pk_test_1", ReadString(ok.Value, "publishableKey"));
        Assert.Equal("pi_1", ReadString(ok.Value, "paymentIntentId"));
        Assert.Equal(state.OrderId, ReadString(ok.Value, "orderId"));
        Assert.Equal(57.25m, ReadDecimal(ok.Value, "amount"));
        Assert.Equal("USD", ReadString(ok.Value, "currency"));
        Assert.Equal(57.25m, state.Total);
        Assert.False(string.IsNullOrWhiteSpace(state.OrderId));
        context.Session.Verify(service => service.SaveState(state), Times.Once);
    }

    private static void SetupReadyCart(TestContext context, User user, decimal total)
    {
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, total));
        context.Payments.SetupGet(service => service.IsConfigured).Returns(true);
    }

    private static TestContext CreateContext(User? currentUser, CheckoutState state)
    {
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);
        var session = new Mock<ICheckoutSessionService>();
        session.Setup(service => service.GetState()).Returns(state);
        var orders = new Mock<ICheckoutOrderService>();
        var payments = new Mock<ICheckoutCardPaymentService>();
        var flow = new CheckoutCardFlowService(session.Object, orders.Object, payments.Object);
        var controller = new CheckoutCardsController(users.Object, flow)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return new TestContext(controller, session, orders, payments, users);
    }

    private static CheckoutState CardState() => new()
    {
        Shipping = Shipping(),
        PaymentMethod = CheckoutPaymentMethod.Card
    };

    private static ShippingInfoVM Shipping() => new()
    {
        FullName = "Test User",
        Address = "1 Test Street",
        City = "Varna",
        PostalCode = "9000",
        Country = "Bulgaria",
        Phone = "+359888000000"
    };

    private static User User() => new() { Id = "user-1" };

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

    private sealed record TestContext(
        CheckoutCardsController Controller,
        Mock<ICheckoutSessionService> Session,
        Mock<ICheckoutOrderService> Orders,
        Mock<ICheckoutCardPaymentService> Payments,
        Mock<UserManager<User>> Users);

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

    private static decimal ReadDecimal(object? value, string propertyName) =>
        (decimal)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0m);
}
