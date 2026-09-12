using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.Store;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CheckoutStatusControllerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("  order-123  ", "order-123")]
    public void Registered_NormalizesOrderIdAndReturnsRegisteredContract(
        string? orderId,
        string expectedOrderId)
    {
        var controller = new CheckoutStatusController();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Registered(orderId));

        Assert.Equal(expectedOrderId, ReadString(ok.Value, "orderId"));
        Assert.Equal("registered", ReadString(ok.Value, "status"));
        Assert.Equal(
            "Your order was registered successfully.",
            ReadString(ok.Value, "message"));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  order-456  ", "order-456")]
    public void Success_NormalizesOrderIdAndReturnsSuccessContract(
        string? orderId,
        string expectedOrderId)
    {
        var controller = new CheckoutStatusController();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Success(orderId));

        Assert.Equal(expectedOrderId, ReadString(ok.Value, "orderId"));
        Assert.Equal("success", ReadString(ok.Value, "status"));
        Assert.Equal("Payment completed successfully.", ReadString(ok.Value, "message"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_WhenMessageMissing_UsesDefaultMessage(string? message)
    {
        var controller = new CheckoutStatusController();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.Failure(message));

        Assert.Equal("failure", ReadString(ok.Value, "status"));
        Assert.Equal("Payment failed.", ReadString(ok.Value, "message"));
    }

    [Fact]
    public void Failure_WhenMessageProvided_TrimsCustomMessage()
    {
        var controller = new CheckoutStatusController();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            controller.Failure("  Card was declined.  "));

        Assert.Equal("failure", ReadString(ok.Value, "status"));
        Assert.Equal("Card was declined.", ReadString(ok.Value, "message"));
    }

    [Fact]
    public void ThanksForPurchasing_ReturnsLegacyFrontendContract()
    {
        var controller = new CheckoutStatusController();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(controller.ThanksForPurchasing());

        Assert.Equal("Thank you for your purchase.", ReadString(ok.Value, "message"));
        Assert.Equal(
            "/checkout/ThanksForPurchasing",
            ReadString(ok.Value, "frontendRoute"));
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
