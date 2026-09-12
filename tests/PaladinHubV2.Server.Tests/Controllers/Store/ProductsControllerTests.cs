using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class ProductsControllerTests
{
    [Fact]
    public void Index_PreservesQueryStringWhenRedirectingToMerchandise()
    {
        var products = new Mock<IProductService>();
        var controller = CreateController(products.Object);
        controller.HttpContext.Request.QueryString =
            new QueryString("?page=2&sort=name");

        IActionResult result = controller.Index();

        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(
            "/Merchandise/List?page=2&sort=name",
            redirect.Url);
    }

    [Fact]
    public async Task Categories_ReturnsServiceCategories()
    {
        var products = new Mock<IProductService>();
        products
            .Setup(service => service.GetCategories())
            .ReturnsAsync(new List<string> { "Armor", "Books" });
        var controller = CreateController(products.Object);

        IActionResult result = await controller.Categories();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        var categories = Assert.IsType<List<string>>(ok.Value);
        Assert.Equal(new[] { "Armor", "Books" }, categories);
    }

    [Fact]
    public async Task DetailsApi_WhenIdIsBlank_ReturnsBadRequestWithoutServiceCall()
    {
        var products = new Mock<IProductService>();
        var controller = CreateController(products.Object);

        IActionResult result = await controller.DetailsApi(
            "   ",
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Product ID is required.", ReadString(badRequest.Value, "message"));
        products.Verify(
            service => service.GetDetailsAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DetailsLegacy_ForwardsTrimmedIdUserAndAdminRole()
    {
        var products = new Mock<IProductService>();
        products
            .Setup(service => service.GetDetailsAsync(
                "sku-1",
                "user-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaladinHub.Models.Products.ProductDetailsViewModel?)null);
        var controller = CreateController(
            products.Object,
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim(ClaimTypes.Role, "Admin")
                    },
                    "test")));

        IActionResult result = await controller.DetailsLegacy(
            "  sku-1  ",
            CancellationToken.None);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Product not found.", ReadString(notFound.Value, "message"));
        products.Verify(
            service => service.GetDetailsAsync(
                "sku-1",
                "user-1",
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ProductsController CreateController(
        IProductService products,
        ClaimsPrincipal? user = null)
    {
        return new ProductsController(products)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;
}
