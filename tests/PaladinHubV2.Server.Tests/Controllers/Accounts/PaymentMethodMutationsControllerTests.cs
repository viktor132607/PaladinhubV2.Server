using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Payments;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class PaymentMethodMutationsControllerTests
{
    [Fact]
    public async Task AddStripe_WhenIdIsBlank_ReturnsBadRequest()
    {
        var (controller, payments, _) = CreateController(new User());

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddPaymentMethodStripe("  "));

        Assert.Equal("Invalid payment method.", ReadString(bad.Value, "message"));
        payments.Verify(service => service.AddStripePaymentMethod(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddStripe_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, payments, _) = CreateController(null);

        IActionResult result = await controller.AddPaymentMethodStripe("pm_123");

        Assert.IsType<UnauthorizedObjectResult>(result);
        payments.Verify(service => service.AddStripePaymentMethod(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddStripe_WhenValid_TrimsIdAndReturnsOk()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.AddStripePaymentMethod(user, "pm_123"))
            .Returns(Task.CompletedTask);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.AddPaymentMethodStripe("  pm_123  "));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("Card added.", ReadString(ok.Value, "message"));
        payments.Verify(service => service.AddStripePaymentMethod(user, "pm_123"), Times.Once);
    }

    [Fact]
    public async Task RemoveLegacy_WhenIdIsBlank_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController(new User());

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.RemovePaymentMethod(" "));

        Assert.Equal("Payment method ID is required.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task RemoveLegacy_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _, _) = CreateController(null);

        Assert.IsType<UnauthorizedObjectResult>(await controller.RemovePaymentMethod("pm1"));
    }

    [Fact]
    public async Task RemoveLegacy_WhenMethodDoesNotExist_ReturnsNotFound()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.RemovePaymentMethod(user, "pm1")).ReturnsAsync(false);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.RemovePaymentMethod(" pm1 "));

        Assert.Equal("Payment method not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task RemoveLegacy_WhenSuccessful_ReturnsOk()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.RemovePaymentMethod(user, "pm1")).ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.RemovePaymentMethod("pm1"));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("Payment method removed.", ReadString(ok.Value, "message"));
    }

    [Fact]
    public async Task RemoveApi_WhenIdIsBlank_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController(new User());

        Assert.IsType<BadRequestObjectResult>(await controller.RemovePaymentMethodApi(" "));
    }

    [Fact]
    public async Task RemoveApi_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _, _) = CreateController(null);

        Assert.IsType<UnauthorizedObjectResult>(await controller.RemovePaymentMethodApi("pm1"));
    }

    [Fact]
    public async Task RemoveApi_WhenMethodDoesNotExist_ReturnsNotFound()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.RemovePaymentMethod(user, "pm1")).ReturnsAsync(false);

        Assert.IsType<NotFoundObjectResult>(await controller.RemovePaymentMethodApi("pm1"));
    }

    [Fact]
    public async Task RemoveApi_WhenSuccessful_ReturnsNoContent()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.RemovePaymentMethod(user, "pm1")).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await controller.RemovePaymentMethodApi(" pm1 "));
        payments.Verify(service => service.RemovePaymentMethod(user, "pm1"), Times.Once);
    }

    [Fact]
    public async Task SetDefault_WhenIdIsBlank_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController(new User());

        Assert.IsType<BadRequestObjectResult>(await controller.SetDefaultPaymentMethod(" "));
    }

    [Fact]
    public async Task SetDefault_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _, _) = CreateController(null);

        Assert.IsType<UnauthorizedObjectResult>(await controller.SetDefaultPaymentMethod("pm1"));
    }

    [Fact]
    public async Task SetDefault_WhenMethodDoesNotExist_ReturnsNotFound()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.SetDefaultPaymentMethod(user, "pm1")).ReturnsAsync(false);

        Assert.IsType<NotFoundObjectResult>(await controller.SetDefaultPaymentMethod("pm1"));
    }

    [Fact]
    public async Task SetDefault_WhenSuccessful_ReturnsOkAndTrimsId()
    {
        var user = new User();
        var (controller, payments, _) = CreateController(user);
        payments.Setup(service => service.SetDefaultPaymentMethod(user, "pm1")).ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.SetDefaultPaymentMethod(" pm1 "));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("Default payment method updated.", ReadString(ok.Value, "message"));
        payments.Verify(service => service.SetDefaultPaymentMethod(user, "pm1"), Times.Once);
    }

    private static (
        PaymentMethodMutationsController Controller,
        Mock<IPaymentMethodsService> Payments,
        Mock<IAccountUiService> Ui) CreateController(User? user)
    {
        var payments = new Mock<IPaymentMethodsService>();
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var controller = new PaymentMethodMutationsController(payments.Object, ui.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, payments, ui);
    }

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
