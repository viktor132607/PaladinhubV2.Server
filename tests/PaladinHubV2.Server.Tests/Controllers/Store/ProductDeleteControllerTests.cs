using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class ProductDeleteControllerTests
{
    [Fact]
    public async Task DeleteApi_WhenIdMissing_ReturnsBadRequestWithoutCallingService()
    {
        var service = new Mock<IProductService>();
        var controller = new ProductDeleteController(service.Object);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.DeleteApi("   "));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        service.Verify(productService => productService.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteLegacy_WhenIdMissing_ReturnsBadRequestWithoutCallingService()
    {
        var service = new Mock<IProductService>();
        var controller = new ProductDeleteController(service.Object);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.DeleteLegacy(""));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        service.Verify(productService => productService.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteApi_WhenProductMissing_TrimsIdAndReturnsNotFound()
    {
        var service = new Mock<IProductService>();
        service.Setup(productService => productService.Delete("p-1"))
            .ReturnsAsync(false);
        var controller = new ProductDeleteController(service.Object);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.DeleteApi("  p-1  "));

        Assert.Equal("Product not found.", ReadString(notFound.Value, "message"));
        service.Verify(productService => productService.Delete("p-1"), Times.Once);
    }

    [Fact]
    public async Task DeleteApi_WhenProductExists_ReturnsNoContent()
    {
        var service = new Mock<IProductService>();
        service.Setup(productService => productService.Delete("p-2"))
            .ReturnsAsync(true);
        var controller = new ProductDeleteController(service.Object);

        Assert.IsType<NoContentResult>(await controller.DeleteApi("p-2"));

        service.Verify(productService => productService.Delete("p-2"), Times.Once);
    }

    [Fact]
    public async Task DeleteLegacy_WhenProductExists_UsesSameDeleteContract()
    {
        var service = new Mock<IProductService>();
        service.Setup(productService => productService.Delete("p-3"))
            .ReturnsAsync(true);
        var controller = new ProductDeleteController(service.Object);

        Assert.IsType<NoContentResult>(await controller.DeleteLegacy("  p-3 "));

        service.Verify(productService => productService.Delete("p-3"), Times.Once);
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
