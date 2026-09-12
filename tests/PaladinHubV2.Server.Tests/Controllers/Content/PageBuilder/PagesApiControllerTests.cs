using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PagesApiControllerTests
{
    [Fact]
    public async Task PutLayout_WhenIdIsInvalid_ReturnsBadRequestWithoutCallingService()
    {
        var pages = new Mock<IPageService>();
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(0, Request());

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid page ID.", ReadString(badRequest.Value, "message"));
        pages.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PutLayout_WhenLayoutIsMissing_ReturnsRequiredMessage()
    {
        var pages = new Mock<IPageService>();
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(1, new PagesApiController.PutLayoutRequest
        {
            JsonLayout = "   ",
            RowVersionBase64 = Convert.ToBase64String(new byte[] { 1 })
        });

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("JsonLayout is required.", ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task PutLayout_WhenRowVersionIsMissing_ReturnsRequiredMessage()
    {
        var pages = new Mock<IPageService>();
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(1, new PagesApiController.PutLayoutRequest
        {
            JsonLayout = "[]",
            RowVersionBase64 = " "
        });

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("RowVersionBase64 is required.", ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task PutLayout_WhenRowVersionIsInvalidBase64_ReturnsBadRequest()
    {
        var pages = new Mock<IPageService>();
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(1, new PagesApiController.PutLayoutRequest
        {
            JsonLayout = "[]",
            RowVersionBase64 = "not-base64!"
        });

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("RowVersionBase64 is invalid.", ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task PutLayout_WhenPageDoesNotExist_ReturnsNotFound()
    {
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(7)).ReturnsAsync((ContentPage?)null);
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(7, Request());

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Page not found.", ReadString(notFound.Value, "message"));
        pages.Verify(service => service.GetByIdAsync(7), Times.Once);
        pages.Verify(
            service => service.UpdateLayoutSafeAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task PutLayout_WhenConcurrencyCheckFails_ReturnsConflict()
    {
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(7)).ReturnsAsync(new ContentPage { Id = 7 });
        pages.Setup(service => service.UpdateLayoutSafeAsync(
                7,
                "[]",
                It.Is<byte[]>(value => value.SequenceEqual(new byte[] { 1, 2, 3 })),
                "Viktor"))
            .ReturnsAsync((false, null));
        var controller = CreateController(pages.Object, "Viktor");

        IActionResult result = await controller.PutLayout(7, Request("  []  "));

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(
            "The page was modified by someone else. Refresh and try again.",
            ReadString(conflict.Value, "message"));
    }

    [Fact]
    public async Task PutLayout_WhenUpdateSucceeds_ReturnsNewRowVersionAndForwardsTrimmedLayout()
    {
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(7)).ReturnsAsync(new ContentPage { Id = 7 });
        pages.Setup(service => service.UpdateLayoutSafeAsync(
                7,
                "[]",
                It.Is<byte[]>(value => value.SequenceEqual(new byte[] { 1, 2, 3 })),
                "admin"))
            .ReturnsAsync((true, new byte[] { 9, 8 }));
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(7, Request("  []  "));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(7, ReadInt(ok.Value, "id"));
        Assert.Equal(Convert.ToBase64String(new byte[] { 9, 8 }), ReadString(ok.Value, "rowVersionBase64"));
        pages.Verify(service => service.UpdateLayoutSafeAsync(
            7,
            "[]",
            It.Is<byte[]>(value => value.SequenceEqual(new byte[] { 1, 2, 3 })),
            "admin"), Times.Once);
    }

    [Fact]
    public async Task PutLayout_WhenLayoutValidationFails_ReturnsErrors()
    {
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(7)).ReturnsAsync(new ContentPage { Id = 7 });
        pages.Setup(service => service.UpdateLayoutSafeAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>()))
            .ThrowsAsync(new JsonLayoutValidationException(new[] { "bad block", "bad tree" }));
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.PutLayout(7, Request());

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Layout validation failed.", ReadString(badRequest.Value, "message"));
        Assert.Equal(new[] { "bad block", "bad tree" }, ReadStrings(badRequest.Value, "errors"));
    }

    [Fact]
    public async Task GetHead_WhenPageDoesNotExist_ReturnsNotFound()
    {
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(3)).ReturnsAsync((ContentPage?)null);
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.GetHead(3);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Page not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task GetHead_WhenPageExists_ReturnsVersionAndTimestamp()
    {
        DateTime updatedAt = new(2026, 9, 12, 18, 0, 0, DateTimeKind.Utc);
        var pages = new Mock<IPageService>();
        pages.Setup(service => service.GetByIdAsync(3)).ReturnsAsync(new ContentPage
        {
            Id = 3,
            RowVersion = new byte[] { 4, 5, 6 },
            UpdatedAt = updatedAt
        });
        var controller = CreateController(pages.Object);

        IActionResult result = await controller.GetHead(3);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, ReadInt(ok.Value, "id"));
        Assert.Equal(Convert.ToBase64String(new byte[] { 4, 5, 6 }), ReadString(ok.Value, "rowVersionBase64"));
        Assert.Equal(updatedAt, ReadDateTime(ok.Value, "updatedAt"));
    }

    private static PagesApiController.PutLayoutRequest Request(string json = "[]") => new()
    {
        JsonLayout = json,
        RowVersionBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 })
    };

    private static PagesApiController CreateController(IPageService pages, string? username = null)
    {
        ClaimsPrincipal user = username == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, username) },
                authenticationType: "unit-test"));

        return new PagesApiController(pages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static object? Read(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value);

    private static string? ReadString(object? value, string propertyName) =>
        Read(value, propertyName) as string;

    private static int ReadInt(object? value, string propertyName) =>
        Assert.IsType<int>(Read(value, propertyName));

    private static DateTime ReadDateTime(object? value, string propertyName) =>
        Assert.IsType<DateTime>(Read(value, propertyName));

    private static IReadOnlyList<string> ReadStrings(object? value, string propertyName) =>
        Assert.IsAssignableFrom<IReadOnlyList<string>>(Read(value, propertyName));
}
