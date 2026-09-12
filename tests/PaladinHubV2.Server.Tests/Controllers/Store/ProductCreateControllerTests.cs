using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class ProductCreateControllerTests
{
    [Fact]
    public async Task Create_ReturnsFormModel()
    {
        var products = new Mock<IProductService>();
        var forms = new Mock<IProductAdminFormService>();
        var model = Product("New product");
        forms.Setup(service => service.BuildCreateModelAsync()).ReturnsAsync(model);
        var controller = new ProductCreateController(products.Object, forms.Object);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Create());

        Assert.Same(model, ok.Value);
        forms.Verify(service => service.BuildCreateModelAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateApi_WhenModelMissing_ReturnsBadRequest()
    {
        var (controller, products, forms) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateApi(null, CancellationToken.None));

        Assert.Equal("Product data is required.", ReadString(bad.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(It.IsAny<CreateProductViewModel>()), Times.Never);
        products.Verify(service => service.Create(It.IsAny<CreateProductViewModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateLegacy_WhenModelMissing_UsesSameValidation()
    {
        var (controller, products, _) = CreateController();

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateLegacy(null, CancellationToken.None));

        Assert.Equal("Product data is required.", ReadString(bad.Value, "message"));
        products.Verify(service => service.Create(It.IsAny<CreateProductViewModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateApi_WhenModelStateInvalid_AppliesCategoryThenReturnsValidationProblem()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("Invalid");
        controller.ModelState.AddModelError("Name", "Invalid name");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(
            await controller.CreateApi(model, CancellationToken.None));

        ValidationProblemDetails details = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.True(details.Errors.ContainsKey("Name"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
        products.Verify(service => service.Create(It.IsAny<CreateProductViewModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateApi_WhenNameConflicts_ReturnsConflict()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("Duplicate");
        products.Setup(service => service.Create(model))
            .ReturnsAsync((CreateProductViewModel)null!);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.CreateApi(model, CancellationToken.None));

        Assert.Equal("Product with this name already exists.", ReadString(conflict.Value, "message"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
        products.Verify(service => service.Create(model), Times.Once);
    }

    [Fact]
    public async Task CreateApi_WhenSuccessful_ReturnsCreatedProduct()
    {
        var (controller, products, forms) = CreateController();
        var model = Product("New product");
        var created = Product("New product");
        products.Setup(service => service.Create(model)).ReturnsAsync(created);

        ObjectResult result = Assert.IsType<ObjectResult>(
            await controller.CreateApi(model, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.True(ReadBoolean(result.Value, "ok"));
        Assert.Same(created, ReadObject(result.Value, "product"));
        forms.Verify(service => service.ApplyNewCategory(model), Times.Once);
        products.Verify(service => service.Create(model), Times.Once);
    }

    private static (
        ProductCreateController Controller,
        Mock<IProductService> Products,
        Mock<IProductAdminFormService> Forms) CreateController()
    {
        var products = new Mock<IProductService>();
        var forms = new Mock<IProductAdminFormService>();
        return (new ProductCreateController(products.Object, forms.Object), products, forms);
    }

    private static CreateProductViewModel Product(string name) => new()
    {
        Name = name,
        Price = 19.99m,
        Category = "Armor"
    };

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static object? ReadObject(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value);

    private static string? ReadString(object? value, string propertyName) =>
        ReadObject(value, propertyName) as string;
}
