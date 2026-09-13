using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageManagementControllerTests
{
    [Fact]
    public async Task List_ReturnsPagesSortedAndCapitalized()
    {
        await using AppDbContext db = CreateContext();
        ContentPage zulu = Page("retribution", "zulu", "Zulu");
        ContentPage beta = Page("holy", "beta", "Beta");
        ContentPage alpha = Page("holy", "alpha", "Alpha");
        db.ContentPages.AddRange(zulu, beta, alpha);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsAssignableFrom<IEnumerable<object>>(result.Value).ToList();

        Assert.Equal(new[] { "Alpha", "Beta", "Zulu" }, rows.Select(row => ControllerTestSupport.ReadString(row, "title")));
        Assert.Equal(new[] { "Holy", "Holy", "Retribution" }, rows.Select(row => ControllerTestSupport.ReadString(row, "section")));
        Assert.Equal(Convert.ToBase64String(alpha.RowVersion), ControllerTestSupport.ReadString(rows[0], "rowVersionBase64"));
    }

    [Fact]
    public async Task Get_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).Get(999, TestContext.Current.CancellationToken));
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Get_ZeroId_UsesNormalNotFoundPath()
    {
        await using AppDbContext db = CreateContext();
        IActionResult result = await CreateController(db).Get(0, TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Get_FoundPage_ReturnsCompletePayload()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("protection", "tank", "Tank");
        page.IsPublished = false;
        page.JsonLayout = "[{\"type\":\"paragraph\"}]";
        page.UpdatedBy = "admin-7";
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).Get(page.Id, TestContext.Current.CancellationToken));

        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal("Protection", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("Tank", ControllerTestSupport.ReadString(result.Value, "title"));
        Assert.Equal("tank", ControllerTestSupport.ReadString(result.Value, "slug"));
        Assert.False(ControllerTestSupport.ReadBoolean(result.Value, "isPublished"));
        Assert.Equal(page.JsonLayout, ControllerTestSupport.ReadString(result.Value, "jsonLayout"));
        Assert.Equal("admin-7", ControllerTestSupport.ReadString(result.Value, "updatedBy"));
        Assert.Equal(Convert.ToBase64String(page.RowVersion), ControllerTestSupport.ReadString(result.Value, "rowVersionBase64"));
    }

    private static ContentPage Page(string section, string slug, string title) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = "[]",
        IsPublished = true,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow,
        RowVersion = Array.Empty<byte>()
    };

    private static PageManagementController CreateController(AppDbContext db) =>
        new(new PageManagementService(db));

    private static AppDbContext CreateContext() =>
        PageBuilderSqliteTestDatabase.CreateContext();
}
