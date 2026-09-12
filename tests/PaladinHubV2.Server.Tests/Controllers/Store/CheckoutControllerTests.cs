using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CheckoutControllerTests
{
    [Fact]
    public void Start_WhenJsonIsAccepted_ReturnsApiRedirect()
    {
        var (controller, _, _, _) = CreateController(null);
        controller.Request.Headers.Accept = "application/json";

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Start());

        Assert.Equal("/Checkout/Shipping", ReadString(ok.Value, "redirect"));
    }

    [Fact]
    public void Start_WhenHtmlIsExpected_RedirectsToConfiguredClient()
    {
        var (controller, _, _, _) = CreateController(null, "https://client.example/");

        RedirectResult redirect = Assert.IsType<RedirectResult>(controller.Start());

        Assert.Equal("https://client.example/Checkout/Shipping", redirect.Url);
    }

    [Fact]
    public void ShippingGet_WhenStateHasNoShipping_ReturnsEmptyModel()
    {
        var state = new CheckoutState();
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Shipping());

        Assert.IsType<ShippingInfoVM>(ok.Value);
    }

    [Fact]
    public void ShippingGet_WhenStateHasShipping_ReturnsStoredShipping()
    {
        var shipping = Shipping();
        var state = new CheckoutState { Shipping = shipping };
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Shipping());

        Assert.Same(shipping, ok.Value);
    }

    [Fact]
    public void ShippingPost_WhenModelStateInvalid_ReturnsValidationProblemWithoutSaving()
    {
        var (controller, session, _, _) = CreateController(null);
        controller.ModelState.AddModelError("FullName", "Required");

        ObjectResult validation = Assert.IsAssignableFrom<ObjectResult>(controller.Shipping(Shipping()));

        ValidationProblemDetails details = Assert.IsType<ValidationProblemDetails>(validation.Value);
        Assert.True(details.Errors.ContainsKey("FullName"));
        session.Verify(service => service.SaveState(It.IsAny<CheckoutState>()), Times.Never);
    }

    [Fact]
    public void ShippingPost_WhenValid_NormalizesAndSavesState()
    {
        var state = new CheckoutState();
        var shipping = Shipping();
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Shipping(shipping));

        session.Verify(service => service.NormalizeShipping(shipping), Times.Once);
        session.Verify(service => service.SaveState(state), Times.Once);
        Assert.Same(shipping, state.Shipping);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("/Checkout/Payment", ReadString(ok.Value, "redirect"));
    }

    [Fact]
    public void PaymentGet_WhenShippingMissing_ReturnsConflict()
    {
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(new CheckoutState());

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(controller.Payment());

        Assert.Equal("Shipping details are required.", ReadString(conflict.Value, "message"));
    }

    [Fact]
    public void PaymentGet_WhenShippingExists_DefaultsToCard()
    {
        var state = new CheckoutState { Shipping = Shipping() };
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Payment());
        PaymentVM payment = Assert.IsType<PaymentVM>(ok.Value);

        Assert.Equal(PaymentMethod.Card, payment.Method);
    }

    [Fact]
    public void PaymentGet_WhenMethodExists_ReturnsStoredMethod()
    {
        var state = new CheckoutState
        {
            Shipping = Shipping(),
            PaymentMethod = PaymentMethod.Balance
        };
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Payment());
        PaymentVM payment = Assert.IsType<PaymentVM>(ok.Value);

        Assert.Equal(PaymentMethod.Balance, payment.Method);
    }

    [Fact]
    public void PaymentPost_WhenModelStateInvalid_ReturnsValidationProblem()
    {
        var (controller, session, _, _) = CreateController(null);
        controller.ModelState.AddModelError("Method", "Invalid");

        ObjectResult validation = Assert.IsAssignableFrom<ObjectResult>(
            controller.Payment(new PaymentVM { Method = PaymentMethod.Card }));

        Assert.IsType<ValidationProblemDetails>(validation.Value);
        session.Verify(service => service.SaveState(It.IsAny<CheckoutState>()), Times.Never);
    }

    [Fact]
    public void PaymentPost_WhenEnumValueInvalid_ReturnsBadRequest()
    {
        var (controller, session, _, _) = CreateController(null);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            controller.Payment(new PaymentVM { Method = (PaymentMethod)999 }));

        Assert.Equal("Invalid payment method.", ReadString(bad.Value, "message"));
        session.Verify(service => service.GetState(), Times.Never);
    }

    [Fact]
    public void PaymentPost_WhenShippingMissing_ReturnsConflict()
    {
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(new CheckoutState());

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            controller.Payment(new PaymentVM { Method = PaymentMethod.Balance }));

        Assert.Equal("/Checkout/Shipping", ReadString(conflict.Value, "redirect"));
        session.Verify(service => service.SaveState(It.IsAny<CheckoutState>()), Times.Never);
    }

    [Fact]
    public void PaymentPost_WhenValid_SavesSelectedMethod()
    {
        var state = new CheckoutState { Shipping = Shipping() };
        var (controller, session, _, _) = CreateController(null);
        session.Setup(service => service.GetState()).Returns(state);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            controller.Payment(new PaymentVM { Method = PaymentMethod.CashOnDelivery }));

        Assert.Equal(PaymentMethod.CashOnDelivery, state.PaymentMethod);
        session.Verify(service => service.SaveState(state), Times.Once);
        Assert.Equal("/Checkout/Review", ReadString(ok.Value, "redirect"));
    }

    [Fact]
    public async Task Review_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, orders, _) = CreateController(null);

        IActionResult result = await controller.Review(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Review_WhenCheckoutStateIncomplete_ReturnsConflict()
    {
        var user = new User { Id = "user-1" };
        var state = new CheckoutState { Shipping = Shipping() };
        var (controller, session, orders, _) = CreateController(user);
        session.Setup(service => service.GetState()).Returns(state);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.Review(CancellationToken.None));

        Assert.Contains("payment method", ReadString(conflict.Value, "message"), StringComparison.OrdinalIgnoreCase);
        orders.Verify(
            service => service.GetCartSnapshotAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0, 25.00)]
    [InlineData(2, 0.00)]
    public async Task Review_WhenCartIsEmpty_ReturnsBadRequest(int items, double total)
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Card);
        var (controller, session, orders, _) = CreateController(user);
        session.Setup(service => service.GetState()).Returns(state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(items, (decimal)total));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Review(CancellationToken.None));

        Assert.Equal("Your cart is empty.", ReadString(bad.Value, "message"));
        Assert.Equal((decimal)total, state.Total);
        session.Verify(service => service.SaveState(state), Times.Once);
        orders.Verify(
            service => service.GetPaymentReviewAsync(It.IsAny<User>(), It.IsAny<CheckoutState>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task Review_WhenValid_ReturnsCheckoutSummaryAndPaymentReview()
    {
        var user = new User { Id = "user-1" };
        var state = CompleteState(PaymentMethod.Balance);
        state.OrderId = "order-123";
        var (controller, session, orders, _) = CreateController(user);
        session.Setup(service => service.GetState()).Returns(state);
        orders.Setup(service => service.GetCartSnapshotAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCartSnapshot(3, 42.50m));
        orders.Setup(service => service.GetPaymentReviewAsync(user, state, 42.50m))
            .ReturnsAsync(new CheckoutPaymentReview(30m, "Insufficient wallet balance."));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Review(CancellationToken.None));

        Assert.Equal(42.50m, ReadDecimal(ok.Value, "total"));
        Assert.Equal(3, ReadInt(ok.Value, "items"));
        Assert.Equal(30m, ReadNullableDecimal(ok.Value, "walletBalance"));
        Assert.Equal("Insufficient wallet balance.", ReadString(ok.Value, "paymentError"));
        Assert.Equal("order-123", ReadString(ok.Value, "orderId"));
        session.Verify(service => service.SaveState(state), Times.Once);
        orders.Verify(service => service.GetPaymentReviewAsync(user, state, 42.50m), Times.Once);
    }

    private static (
        CheckoutController Controller,
        Mock<ICheckoutSessionService> Session,
        Mock<ICheckoutOrderService> Orders,
        Mock<UserManager<User>> Users) CreateController(
        User? currentUser,
        string? clientBaseUrl = null)
    {
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);

        var session = new Mock<ICheckoutSessionService>();
        var orders = new Mock<ICheckoutOrderService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClientApp:BaseUrl"] = clientBaseUrl
            })
            .Build();

        var controller = new CheckoutController(
            users.Object,
            session.Object,
            orders.Object,
            configuration)
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

    private static ShippingInfoVM Shipping() => new()
    {
        FullName = "Test User",
        Address = "1 Test Street",
        City = "Varna",
        PostalCode = "9000",
        Country = "Bulgaria",
        Phone = "+359888000000",
        Email = "test@example.com"
    };

    private static CheckoutState CompleteState(PaymentMethod method) => new()
    {
        Shipping = Shipping(),
        PaymentMethod = method
    };

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

    private static decimal ReadDecimal(object? value, string propertyName) =>
        (decimal)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0m);

    private static decimal? ReadNullableDecimal(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as decimal?;

    private static int ReadInt(object? value, string propertyName) =>
        (int)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0);
}
