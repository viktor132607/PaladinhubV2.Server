using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Payments;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class PaymentMethodsControllerTests
{
    [Fact]
    public async Task PaymentMethods_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, paymentMethods, ui) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.PaymentMethods());

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
        paymentMethods.Verify(service => service.GetMethods(It.IsAny<User>()), Times.Never);
        ui.Verify(service => service.GetBalance(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "EU", "EUR", "Europe")]
    [InlineData("US", "US", "USD", "United States")]
    public async Task PaymentMethods_UsesRegionAndProjectsMethods(
        string? cookieRegion,
        string expectedRegionCode,
        string currency,
        string regionDisplay)
    {
        var user = new User { Id = "user-1" };
        var (controller, paymentMethods, ui) = CreateController(user);
        ui.Setup(service => service.ReadRegionCookie()).Returns(cookieRegion);
        ui.Setup(service => service.GetCurrencyForRegion(expectedRegionCode)).Returns(currency);
        ui.Setup(service => service.RegionDisplay(expectedRegionCode)).Returns(regionDisplay);
        ui.Setup(service => service.GetBalance("user-1")).ReturnsAsync(42.50m);
        paymentMethods.Setup(service => service.GetMethods(user)).ReturnsAsync(new List<PaymentMethod>
        {
            new()
            {
                Id = "pm-1",
                UserId = "user-1",
                Brand = "Visa",
                Last4 = "4242",
                Label = "Main",
                IsDefault = true,
                ExternalId = "ext-1",
                Provider = "Stripe"
            }
        });

        OkObjectResult result = Assert.IsType<OkObjectResult>(await controller.PaymentMethods());

        Assert.Equal(expectedRegionCode, ReadString(result.Value, "regionCode"));
        Assert.Equal(regionDisplay, ReadString(result.Value, "region"));
        Assert.Equal(currency, ReadString(result.Value, "currency"));
        Assert.Equal(42.50m, ReadDecimal(result.Value, "balance"));

        IEnumerable projected = Assert.IsAssignableFrom<IEnumerable>(Read(result.Value, "methods"));
        object method = Assert.Single(projected.Cast<object>());
        Assert.Equal("pm-1", ReadString(method, "Id"));
        Assert.Equal("Visa", ReadString(method, "Brand"));
        Assert.Equal("4242", ReadString(method, "Last4"));
        Assert.True(ReadBoolean(method, "IsDefault"));
    }

    [Fact]
    public async Task AddPaymentMethod_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, paymentMethods, _) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.AddPaymentMethod());

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
        paymentMethods.Verify(service => service.GetStripePublishableKey(), Times.Never);
    }

    [Fact]
    public async Task AddPaymentMethod_WhenStripeKeyMissing_ReturnsServiceUnavailable()
    {
        var user = new User { Id = "user-1" };
        var (controller, paymentMethods, _) = CreateController(user);
        paymentMethods.Setup(service => service.GetStripePublishableKey()).Returns("   ");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.AddPaymentMethod());

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(
            "Stripe publishable key is not configured.",
            ReadString(result.Value, "message"));
        paymentMethods.Verify(service => service.EnsureStripeCustomer(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task AddPaymentMethod_WhenConfigured_ReturnsKeyAndCustomer()
    {
        var user = new User { Id = "user-1" };
        var (controller, paymentMethods, _) = CreateController(user);
        paymentMethods.Setup(service => service.GetStripePublishableKey()).Returns("pk_test_123");
        paymentMethods.Setup(service => service.EnsureStripeCustomer(user)).ReturnsAsync("cus_123");

        OkObjectResult result = Assert.IsType<OkObjectResult>(await controller.AddPaymentMethod());

        Assert.Equal("pk_test_123", ReadString(result.Value, "publishableKey"));
        Assert.Equal("cus_123", ReadString(result.Value, "customerId"));
        paymentMethods.Verify(service => service.EnsureStripeCustomer(user), Times.Once);
    }

    private static (
        PaymentMethodsController Controller,
        Mock<IPaymentMethodsService> PaymentMethods,
        Mock<IAccountUiService> Ui) CreateController(User? user)
    {
        var paymentMethods = new Mock<IPaymentMethodsService>();
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        return (new PaymentMethodsController(paymentMethods.Object, ui.Object), paymentMethods, ui);
    }

    private static object? Read(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value);

    private static string? ReadString(object? value, string propertyName) =>
        Read(value, propertyName) as string;

    private static decimal ReadDecimal(object? value, string propertyName) =>
        Read(value, propertyName) is decimal result ? result : 0m;

    private static bool ReadBoolean(object? value, string propertyName) =>
        Read(value, propertyName) is true;
}
