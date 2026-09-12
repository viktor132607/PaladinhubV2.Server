using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class ProductEditControllerTests
{
    [Fact]
    public async Task EditApiGet_WhenIdMissing_ReturnsBadRequest()
    {
        var (controller, _, forms) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.EditApi("   ", CancellationToken.None));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        forms.Verify(
            service => service.BuildEditModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditApiGet_TrimsIdAndReturnsModel()
    {
        var (controller, _, forms) = CreateController();
        var model = Product("p-1");
        forms.Setup(service => service.BuildEditModelAsync("p-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.EditApi("  p-1  ", CancellationToken.None));

        Assert.Same(model, ok.Value);
        forms.Verify(
            service => service.BuildEditModelAsync("p-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditLegacyGet_WhenProductMissing_ReturnsNotFound()
    {
        var (controller, _, forms) = CreateController();
        forms.Setup(service => service.BuildEditModelAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EditProductViewModel?)null);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.EditLegacy("missing", CancellationToken.None));

        Assert.Equal("Product not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task EditApiPut_WhenRouteIdDiffers_ReturnsBadRequestBeforeMutation()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("body-id");

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.EditApi("route-id", model, CancellationToken.None));

        Assert.Equal(
            "The route product ID does not match the request product ID.",
            ReadString(bad.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(It.IsAny<EditProductViewModel>()), Times.Never);
        products.Verify(
            service => service.UpdateAsync(It.IsAny<EditProductViewModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditLegacy_WhenModelMissing_ReturnsBadRequest()
    {
        var (controller, products, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.EditLegacy(null, CancellationToken.None));

        Assert.Equal("Product data is required.", ReadString(bad.Value, "message"));
        products.Verify(
            service => service.UpdateAsync(It.IsAny<EditProductViewModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditLegacy_WhenProductIdMissing_ReturnsBadRequest()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("   ");

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.EditLegacy(model, CancellationToken.None));

        Assert.Equal("Product ID is required.", ReadString(bad.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(It.IsAny<EditProductViewModel>()), Times.Never);
        products.Verify(
            service => service.UpdateAsync(It.IsAny<EditProductViewModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditLegacy_WhenModelStateInvalid_ReturnsValidationProblem()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("p-1");
        controller.ModelState.AddModelError("Name", "Invalid name");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(
            await controller.EditLegacy(model, CancellationToken.None));

        ValidationProblemDetails details = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.True(details.Errors.ContainsKey("Name"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
        products.Verify(
            service => service.UpdateAsync(It.IsAny<EditProductViewModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditApiPut_WhenUpdateFails_ReturnsConflict()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("p-1");
        products.Setup(service => service.UpdateAsync(model, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.EditApi("p-1", model, CancellationToken.None));

        Assert.Equal(
            "Product was not found or another product already uses this name.",
            ReadString(conflict.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
    }

    [Fact]
    public async Task EditApiPut_WhenSuccessful_ReturnsUpdatedId()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("p-1");
        products.Setup(service => service.UpdateAsync(model, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.EditApi("p-1", model, CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("p-1", ReadString(ok.Value, "id"));
        Assert.Equal("Product updated successfully.", ReadString(ok.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
        products.Verify(service => service.UpdateAsync(model, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (
        ProductEditController Controller,
        Mock<IProductService> Products,
        Mock<IProductAdminFormService> Forms) CreateController()
    {
        var products = new Mock<IProductService>();
        var forms = new Mock<IProductAdminFormService>();
        return (new ProductEditController(products.Object, forms.Object), products, forms);
    }

    private static EditProductViewModel Product(string id) => new()
    {
        Id = id,
        Name = "Product",
        Price = 24.99m,
        Category = "Armor"
    };

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
