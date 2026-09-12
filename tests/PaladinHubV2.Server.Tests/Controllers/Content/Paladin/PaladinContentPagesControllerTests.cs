using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.API.Controllers.Content.Paladin;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Domain.Services.TalentTrees;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.Paladin;

public sealed class PaladinContentPagesControllerTests
{
    [Fact]
    public async Task Page_BlankSlug_ReturnsBadRequestWithoutLookup()
    {
        var fixture = CreateFixture();

        IActionResult result = await fixture.Controller.Page("holy", "   ");

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Page slug is required.", ControllerTestSupport.ReadString(bad.Value, "message"));
        fixture.Pages.Verify(
            x => x.GetByRouteAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Page_MissingPage_ReturnsNotFoundWithNormalizedRoute()
    {
        var fixture = CreateFixture();
        fixture.Pages.Setup(x => x.GetByRouteAsync("protection", "rotation"))
            .ReturnsAsync((ContentPage?)null);

        IActionResult result = await fixture.Controller.Page("  PROT  ", "  Rotation  ");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(notFound.Value, "message"));
        fixture.Pages.Verify(x => x.GetByRouteAsync("protection", "rotation"), Times.Once);
    }

    [Fact]
    public async Task Page_UnpublishedPage_ReturnsNotFound()
    {
        var fixture = CreateFixture();
        fixture.Pages.Setup(x => x.GetByRouteAsync("holy", "guide"))
            .ReturnsAsync(CreatePage(isPublished: false));

        IActionResult result = await fixture.Controller.Page("holy", "guide");

        Assert.IsType<NotFoundObjectResult>(result);
        fixture.Renderer.Verify(x => x.RenderAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Page_Success_NormalizesAliasesSetsSessionAndReturnsRenderedPage()
    {
        var fixture = CreateFixture(isAdmin: false);
        ContentPage page = CreatePage();
        fixture.Pages.Setup(x => x.GetByRouteAsync("retribution", "guide")).ReturnsAsync(page);
        fixture.Renderer.Setup(x => x.RenderAsync(page.JsonLayout)).ReturnsAsync("<p>Rendered</p>");

        IActionResult result = await fixture.Controller.Page(" RET ", " Guide ");

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ContentPageResponse>(ok.Value);
        Assert.Equal("Retribution", response.Page.Section);
        Assert.Equal("guide", response.Page.Slug);
        Assert.Equal("<p>Rendered</p>", response.Html);
        Assert.False(response.CanEdit);
        Assert.Null(response.RenderError);
        Assert.Equal(Convert.ToBase64String(page.RowVersion), response.Page.RowVersionBase64);
        Assert.Equal("retribution", fixture.Session.GetString("current-section"));
    }

    [Fact]
    public async Task Page_AdminUser_CanEdit()
    {
        var fixture = CreateFixture(isAdmin: true);
        ContentPage page = CreatePage();
        fixture.Pages.Setup(x => x.GetByRouteAsync("holy", "guide")).ReturnsAsync(page);
        fixture.Renderer.Setup(x => x.RenderAsync(page.JsonLayout)).ReturnsAsync("<p>Rendered</p>");

        IActionResult result = await fixture.Controller.Page("holy", "guide");

        var response = Assert.IsType<ContentPageResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(response.CanEdit);
        Assert.Equal("holy", fixture.Session.GetString("current-section"));
    }

    [Fact]
    public async Task Page_RendererFailure_ReturnsPageWithRenderError()
    {
        var fixture = CreateFixture();
        ContentPage page = CreatePage();
        fixture.Pages.Setup(x => x.GetByRouteAsync("holy", "guide")).ReturnsAsync(page);
        fixture.Renderer.Setup(x => x.RenderAsync(page.JsonLayout))
            .ThrowsAsync(new InvalidOperationException("invalid layout"));

        IActionResult result = await fixture.Controller.Page("holy", "guide");

        var response = Assert.IsType<ContentPageResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(string.Empty, response.Html);
        Assert.Equal("invalid layout", response.RenderError);
        Assert.Equal("holy", fixture.Session.GetString("current-section"));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public async Task Page_UnsupportedSection_ThrowsArgumentException(string section)
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Controller.Page(section, "guide"));
    }

    private static ContentFixture CreateFixture(bool isAdmin = false)
    {
        var pages = new Mock<IPageService>();
        var renderer = new Mock<IBlockRenderer>();
        var content = new PaladinContentService(
            Mock.Of<ISpellbookService>(),
            Mock.Of<IItemsService>(),
            new HolySectionService(),
            new ProtectionSectionService(),
            new RetributionSectionService(),
            pages.Object,
            Mock.Of<ITalentTreeService>(),
            renderer.Object);

        var controller = new PaladinContentPagesController(content);
        var session = new TestSession();
        ControllerTestSupport.Attach(
            controller,
            ControllerTestSupport.CreateHttpContext(
                userId: isAdmin ? "admin-1" : null,
                isAdmin: isAdmin,
                session: session));

        return new ContentFixture(controller, pages, renderer, session);
    }

    private static ContentPage CreatePage(bool isPublished = true) => new()
    {
        Id = 15,
        Section = "retribution",
        Slug = "guide",
        Title = "Guide",
        JsonLayout = "[{\"type\":\"paragraph\"}]",
        IsPublished = isPublished,
        UpdatedAt = DateTime.UtcNow,
        UpdatedBy = "admin",
        RowVersion = new byte[] { 1, 2, 3, 4 }
    };

    private sealed record ContentFixture(
        PaladinContentPagesController Controller,
        Mock<IPageService> Pages,
        Mock<IBlockRenderer> Renderer,
        TestSession Session);
}
