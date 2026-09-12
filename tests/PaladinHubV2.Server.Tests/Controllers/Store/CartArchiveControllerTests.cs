using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CartArchiveControllerTests
{
    [Fact]
    public async Task Archive_ProjectsArchiveRowsAndFallbacks()
    {
        var carts = new List<CartViewModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "u1",
                User = new User { UserName = "Viktor" },
                OrderDate = "2026-09-12"
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "u2",
                User = null,
                OrderDate = null
            }
        };
        var service = new Mock<ICartService>();
        service.Setup(value => value.GetArchive()).ReturnsAsync(carts);
        var controller = new CartArchiveController(service.Object);

        OkObjectResult result = Assert.IsType<OkObjectResult>(await controller.Archive());

        IEnumerable response = Assert.IsAssignableFrom<IEnumerable>(result.Value);
        object[] rows = response.Cast<object>().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("Viktor", ReadString(rows[0], "username"));
        Assert.Equal("2026-09-12", ReadString(rows[0], "orderDate"));
        Assert.Equal("Unknown", ReadString(rows[1], "username"));
        Assert.Equal(string.Empty, ReadString(rows[1], "orderDate"));
    }

    [Fact]
    public async Task Details_WhenIdEmpty_ReturnsBadRequestWithoutLookup()
    {
        var service = new Mock<ICartService>();
        var controller = new CartArchiveController(service.Object);

        BadRequestObjectResult result = Assert.IsType<BadRequestObjectResult>(
            await controller.Details(Guid.Empty));

        Assert.Equal("Invalid cart ID.", ReadString(result.Value, "message"));
        service.Verify(value => value.GetCartById(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Details_WhenCartMissing_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        var service = new Mock<ICartService>();
        service.Setup(value => value.GetCartById(id)).ReturnsAsync((MyCartViewModel?)null);
        var controller = new CartArchiveController(service.Object);

        NotFoundObjectResult result = Assert.IsType<NotFoundObjectResult>(
            await controller.Details(id));

        Assert.Equal("Archived cart not found.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Details_WhenCartExists_ReturnsCart()
    {
        Guid id = Guid.NewGuid();
        var cart = new MyCartViewModel { TotalPrice = 99m };
        var service = new Mock<ICartService>();
        service.Setup(value => value.GetCartById(id)).ReturnsAsync(cart);
        var controller = new CartArchiveController(service.Object);

        OkObjectResult result = Assert.IsType<OkObjectResult>(await controller.Details(id));

        Assert.Same(cart, result.Value);
        service.Verify(value => value.GetCartById(id), Times.Once);
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
