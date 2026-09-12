using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageBlocksControllerTests
{
    [Fact]
    public async Task Render_WrapsSingleBlockInArrayAndReturnsHtml()
    {
        var renderer = new Mock<IBlockRenderer>();
        renderer.Setup(x => x.RenderAsync("[{\"type\":\"paragraph\",\"text\":\"Hello\"}]"))
            .ReturnsAsync("<p>Hello</p>");
        var controller = new PageBlocksController(renderer.Object);
        using JsonDocument document = JsonDocument.Parse("{\"type\":\"paragraph\",\"text\":\"Hello\"}");

        IActionResult result = await controller.Render(document.RootElement.Clone());

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("<p>Hello</p>", content.Content);
        Assert.Equal("text/html", content.ContentType);
        renderer.Verify(
            x => x.RenderAsync("[{\"type\":\"paragraph\",\"text\":\"Hello\"}]"),
            Times.Once);
    }

    [Fact]
    public async Task RenderLayout_ForwardsRawLayoutAndReturnsHtml()
    {
        const string json = "[{\"type\":\"heading\",\"level\":2}]";
        var renderer = new Mock<IBlockRenderer>();
        renderer.Setup(x => x.RenderAsync(json)).ReturnsAsync("<h2></h2>");
        var controller = new PageBlocksController(renderer.Object);
        using JsonDocument document = JsonDocument.Parse(json);

        IActionResult result = await controller.RenderLayout(document.RootElement.Clone());

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("<h2></h2>", content.Content);
        Assert.Equal("text/html", content.ContentType);
        renderer.Verify(x => x.RenderAsync(json), Times.Once);
    }

    [Fact]
    public async Task RenderLayout_RendererFailure_IsNotSwallowed()
    {
        const string json = "[]";
        var renderer = new Mock<IBlockRenderer>();
        renderer.Setup(x => x.RenderAsync(json)).ThrowsAsync(new InvalidOperationException("render failed"));
        var controller = new PageBlocksController(renderer.Object);
        using JsonDocument document = JsonDocument.Parse(json);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.RenderLayout(document.RootElement.Clone()));

        Assert.Equal("render failed", error.Message);
    }
}
