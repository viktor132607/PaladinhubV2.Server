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

public sealed class CheckoutCardFinalizeControllerTests
{
    [Fact]
    public async Task CardFinalize_WhenPaymentIntentMissing_ReturnsBadRequestBeforeAuthLookup()
    {
        var context = CreateContext(User(), CardState("order-1"));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await context.Controller.CardFinalize(
                new CheckoutCardFinalizeController.CardFinalizeRequest { PaymentIntentId = "   " },
                CancellationToken.None));

        Assert.Equal("Payment intent ID is required.", ReadString(bad.Value, "message"));
        context.Users.Verify(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
        context.Session.Verify(service => service.GetState(), Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenUserMissing_ReturnsUnauthorized()
    {
        var context = CreateContext(null, CardState("order-1"));

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(
            await Finalize(context));

        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        context.Session.Verify(service => service.GetState(), Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenCardNotSelected_ReturnsBadRequest()
    {
        var state = new CheckoutState
        {
            Shipping = Shipping(),
            PaymentMethod = CheckoutPaymentMethod.Balance,
            OrderId = "order-1"
        };
        var context = CreateContext(User(), state);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(await Finalize(context));

        Assert.Equal("Card payment is not selected.", ReadString(bad.Value, "message"));
        context.Payments.Verify(
            service => service.VerifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenOrderIdMissing_ReturnsBadRequest()
    {
        var context = CreateContext(User(), CardState(null));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(await Finalize(context));

        Assert.Equal("Checkout order ID is missing.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task CardFinalize_WhenOrderAlreadyProcessed_IsIdempotentAndReturnsSuccess()
    {
        var user = User();
        var context = CreateContext(user, CardState("order-1"));
        context.Orders
            .Setup(service => service.OrderTransactionExistsAsync(
                user.Id,
                "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await Finalize(context));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("order-1", ReadString(ok.Value, "orderId"));
        Assert.Equal("/Checkout/Success?orderId=order-1", ReadString(ok.Value, "redirect"));
        context.Orders.Verify(service => service.ArchiveCartAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        context.Session.Verify(service => service.Clear(), Times.Once);
        context.Payments.Verify(
            service => service.VerifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(CheckoutCardPaymentError.PaymentNotCompleted, "Payment was not completed.")]
    [InlineData(CheckoutCardPaymentError.CurrencyMismatch, "Payment currency does not match the order.")]
    [InlineData(CheckoutCardPaymentError.OrderMismatch, "Payment order does not match the checkout order.")]
    [InlineData(CheckoutCardPaymentError.UserMismatch, "Payment user does not match the checkout user.")]
    public async Task CardFinalize_MapsKnownVerificationErrors(
        CheckoutCardPaymentError error,
        string expectedMessage)
    {
        var user = User();
        var context = CreateContext(user, CardState("order-1"));
        SetupUnprocessedOrder(context, user);
        context.Payments
            .Setup(service => service.VerifyAsync(
                "pi_1",
                user.Id,
                "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardVerificationResult(error));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(await Finalize(context));

        Assert.Equal(expectedMessage, ReadString(bad.Value, "message"));
        context.Orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.Session.Verify(service => service.Clear(), Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenVerificationFailsUnexpectedly_ReturnsBadGateway()
    {
        var user = User();
        var context = CreateContext(user, CardState("order-1"));
        SetupUnprocessedOrder(context, user);
        context.Payments
            .Setup(service => service.VerifyAsync(
                "pi_1",
                user.Id,
                "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardVerificationResult(CheckoutCardPaymentError.VerificationFailed));

        ObjectResult error = Assert.IsType<ObjectResult>(await Finalize(context));

        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal("Stripe payment could not be verified.", ReadString(error.Value, "message"));
    }

    [Theory]
    [InlineData(0, 25.0)]
    [InlineData(2, 0.0)]
    public async Task CardFinalize_WhenCartIsEmptyAfterPayment_ReturnsBadRequest(int items, double total)
    {
        var user = User();
        var context = CreateContext(user, CardState("order-1"));
        SetupVerifiedPayment(context, user, 2500L);
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(items, (decimal)total));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(await Finalize(context));

        Assert.Equal("Your cart is empty.", ReadString(bad.Value, "message"));
        context.Payments.Verify(
            service => service.AmountMatches(It.IsAny<decimal>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenCartTotalChanged_ReturnsConflict()
    {
        var user = User();
        var context = CreateContext(user, CardState("order-1"));
        SetupVerifiedPayment(context, user, 2500L);
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(2, 30m));
        context.Payments.Setup(service => service.AmountMatches(30m, 2500L)).Returns(false);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(await Finalize(context));

        Assert.Equal(
            "The cart total changed after the payment session was created.",
            ReadString(conflict.Value, "message"));
        context.Orders.Verify(
            service => service.CompleteCardOrderAsync(
                It.IsAny<User>(), It.IsAny<CheckoutState>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.Session.Verify(service => service.Clear(), Times.Never);
    }

    [Fact]
    public async Task CardFinalize_WhenSuccessful_CompletesOrderSavesTotalAndClearsState()
    {
        var user = User();
        var state = CardState("order / 1");
        var context = CreateContext(user, state);
        context.Orders
            .Setup(service => service.OrderTransactionExistsAsync(
                user.Id,
                "order / 1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        context.Payments
            .Setup(service => service.VerifyAsync(
                "pi_1",
                user.Id,
                "order / 1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardVerificationResult(CheckoutCardPaymentError.None, 4250L));
        context.Orders
            .Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(3, 42.50m));
        context.Payments.Setup(service => service.AmountMatches(42.50m, 4250L)).Returns(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await Finalize(context));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("order / 1", ReadString(ok.Value, "orderId"));
        Assert.Equal("/Checkout/Success?orderId=order%20%2F%201", ReadString(ok.Value, "redirect"));
        Assert.Equal(42.50m, state.Total);
        context.Session.Verify(service => service.SaveState(state), Times.Once);
        context.Orders.Verify(
            service => service.CompleteCardOrderAsync(user, state, It.IsAny<CancellationToken>()),
            Times.Once);
        context.Session.Verify(service => service.Clear(), Times.Once);
    }

    private static Task<IActionResult> Finalize(TestContext context) =>
        context.Controller.CardFinalize(
            new CheckoutCardFinalizeController.CardFinalizeRequest { PaymentIntentId = "pi_1" },
            CancellationToken.None);

    private static void SetupUnprocessedOrder(TestContext context, User user)
    {
        context.Orders
            .Setup(service => service.OrderTransactionExistsAsync(
                user.Id,
                "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static void SetupVerifiedPayment(TestContext context, User user, long amount)
    {
        SetupUnprocessedOrder(context, user);
        context.Payments
            .Setup(service => service.VerifyAsync(
                "pi_1",
                user.Id,
                "order-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCardVerificationResult(CheckoutCardPaymentError.None, amount));
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
        var controller = new CheckoutCardFinalizeController(users.Object, flow)
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

    private static CheckoutState CardState(string? orderId) => new()
    {
        Shipping = Shipping(),
        PaymentMethod = CheckoutPaymentMethod.Card,
        OrderId = orderId
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
        CheckoutCardFinalizeController Controller,
        Mock<ICheckoutSessionService> Session,
        Mock<ICheckoutOrderService> Orders,
        Mock<ICheckoutCardPaymentService> Payments,
        Mock<UserManager<User>> Users);

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
