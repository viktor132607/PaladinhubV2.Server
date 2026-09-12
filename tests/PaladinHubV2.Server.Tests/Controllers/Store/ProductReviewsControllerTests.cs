using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Domain.Services.Products;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class ProductReviewsControllerTests
{
    [Fact]
    public async Task AddReviewApi_WhenRouteIdDoesNotMatch_ReturnsBadRequestWithoutServiceCall()
    {
        var (controller, products) = CreateController("user-1");
        var input = Review("product-2");

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddReviewApi("product-1", input, CancellationToken.None));

        Assert.Equal("The route product ID does not match the review product ID.", ReadString(bad.Value, "message"));
        products.Verify(service => service.AddReviewAsync(It.IsAny<AddReviewInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewApi_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, products) = CreateController(null);

        IActionResult result = await controller.AddReviewApi("product-1", Review("product-1"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        products.Verify(service => service.AddReviewAsync(It.IsAny<AddReviewInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewApi_WhenInputIsNull_ReturnsBadRequest()
    {
        var (controller, _) = CreateController("user-1");

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddReviewApi("product-1", null, CancellationToken.None));

        Assert.Equal("Review data is required.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task AddReviewApi_WhenModelStateIsInvalid_ReturnsValidationProblem()
    {
        var (controller, products) = CreateController("user-1");
        controller.ModelState.AddModelError("Rating", "Rating is invalid.");

        IActionResult result = await controller.AddReviewApi("product-1", Review("product-1"), CancellationToken.None);

        ObjectResult validation = Assert.IsAssignableFrom<ObjectResult>(result);
        ValidationProblemDetails details = Assert.IsType<ValidationProblemDetails>(validation.Value);
        Assert.True(details.Errors.ContainsKey("Rating"));
        Assert.Equal("Rating is invalid.", details.Errors["Rating"].Single());
        products.Verify(service => service.AddReviewAsync(It.IsAny<AddReviewInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewApi_WhenServiceRejects_ReturnsConflict()
    {
        var input = Review("product-1");
        var (controller, products) = CreateController("user-1");
        products.Setup(service => service.AddReviewAsync(input, "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.AddReviewApi("product-1", input, CancellationToken.None));

        Assert.Contains("review only products", ReadString(conflict.Value, "message"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddReviewApi_WhenSuccessful_ReturnsCreatedPayload()
    {
        var input = Review("product-1");
        var (controller, products) = CreateController("user-1");
        products.Setup(service => service.AddReviewAsync(input, "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ObjectResult created = Assert.IsType<ObjectResult>(
            await controller.AddReviewApi("product-1", input, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.True(ReadBoolean(created.Value, "ok"));
        Assert.Equal("product-1", ReadString(created.Value, "productId"));
    }

    [Fact]
    public async Task AddReviewLegacy_WhenSuccessful_CoversLegacyAlias()
    {
        var input = Review("legacy-product");
        var (controller, products) = CreateController("user-1");
        products.Setup(service => service.AddReviewAsync(input, "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ObjectResult created = Assert.IsType<ObjectResult>(
            await controller.AddReviewLegacy(input, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task DeleteReviewApi_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, products) = CreateController(null);

        IActionResult result = await controller.DeleteReviewApi("product-1", 1, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        products.Verify(service => service.DeleteReviewAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteReviewApi_WhenIdIsInvalid_ReturnsBadRequest()
    {
        var (controller, _) = CreateController("user-1");

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.DeleteReviewApi("product-1", 0, CancellationToken.None));

        Assert.Equal("Invalid review ID.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task DeleteReviewApi_WhenServiceRejects_ReturnsForbidden()
    {
        var (controller, products) = CreateController("user-1");
        products.Setup(service => service.DeleteReviewAsync(4, "user-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(
            await controller.DeleteReviewApi("product-1", 4, CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Contains("not allowed", ReadString(forbidden.Value, "message"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteReviewApi_WhenAdminAndSuccessful_ReturnsNoContentAndForwardsAdminFlag()
    {
        var (controller, products) = CreateController("admin-1", isAdmin: true);
        products.Setup(service => service.DeleteReviewAsync(7, "admin-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Assert.IsType<NoContentResult>(
            await controller.DeleteReviewApi("product-1", 7, CancellationToken.None));

        products.Verify(service => service.DeleteReviewAsync(7, "admin-1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteReviewLegacy_WhenSuccessful_CoversLegacyAlias()
    {
        var (controller, products) = CreateController("user-1");
        products.Setup(service => service.DeleteReviewAsync(9, "user-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Assert.IsType<NoContentResult>(
            await controller.DeleteReviewLegacy(9, "legacy-product", CancellationToken.None));
    }

    private static AddReviewInput Review(string productId) => new()
    {
        ProductId = productId,
        Rating = 5,
        Content = "Great product."
    };

    private static (ProductReviewsController Controller, Mock<IProductService> Products) CreateController(
        string? userId,
        bool isAdmin = false)
    {
        var products = new Mock<IProductService>();
        var claims = new List<Claim>();
        if (userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, userId == null ? null : "UnitTest");
        var controller = new ProductReviewsController(products.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return (controller, products);
    }

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
