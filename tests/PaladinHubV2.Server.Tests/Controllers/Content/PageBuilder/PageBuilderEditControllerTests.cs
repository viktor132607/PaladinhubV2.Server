using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageBuilderEditControllerTests
{
    [Fact]
    public async Task Edit_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<NotFoundObjectResult>(await controller.Edit("Holy", "missing"));

        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_FoundPage_ReturnsFullEditorPayload()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("protection", "tank-guide", "Tank Guide");
        page.IsPublished = false;
        page.JsonLayout = "[{\"type\":\"paragraph\"}]";
        page.UpdatedBy = "editor";
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(" prot ", " Tank-Guide "));

        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal("Protection", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("Tank Guide", ControllerTestSupport.ReadString(result.Value, "title"));
        Assert.Equal("tank-guide", ControllerTestSupport.ReadString(result.Value, "slug"));
        Assert.False(ControllerTestSupport.ReadBoolean(result.Value, "isPublished"));
        Assert.Equal(page.JsonLayout, ControllerTestSupport.ReadString(result.Value, "jsonLayout"));
        Assert.Equal(Convert.ToBase64String(page.RowVersion), ControllerTestSupport.ReadString(result.Value, "rowVersionBase64"));
    }

    [Fact]
    public async Task EditPost_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<NotFoundObjectResult>(await controller.EditPost(new EditPageRequest
        {
            Section = "Holy",
            Slug = "missing",
            Title = "Updated",
            JsonLayout = "[]"
        }));

        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task EditPost_ValidRequest_TrimsUpdatesAndReturnsRedirect()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("retribution", "damage", "Old");
        page.JsonLayout = "[]";
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        DateTime before = page.UpdatedAt;
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.EditPost(new EditPageRequest
        {
            Section = " Ret ",
            Slug = " DAMAGE ",
            Title = "  New title  ",
            JsonLayout = "  [{\"type\":\"heading\"}]  "
        }));

        Assert.Equal("New title", page.Title);
        Assert.Equal("[{\"type\":\"heading\"}]", page.JsonLayout);
        Assert.True(page.UpdatedAt >= before);
        Assert.Equal("Retribution", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("/Retribution/damage", ControllerTestSupport.ReadString(result.Value, "redirectUrl"));
    }

    [Fact]
    public async Task EditPost_BlankOptionalChanges_KeepExistingValues()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "healing", "Existing title");
        page.JsonLayout = "[{\"type\":\"paragraph\"}]";
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        IActionResult action = await controller.EditPost(new EditPageRequest
        {
            Section = "Holy",
            Slug = "healing",
            Title = "   ",
            JsonLayout = "   "
        });

        Assert.IsType<OkObjectResult>(action);
        Assert.Equal("Existing title", page.Title);
        Assert.Equal("[{\"type\":\"paragraph\"}]", page.JsonLayout);
    }

    private static ContentPage Page(string section, string slug, string title) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        IsPublished = true,
        JsonLayout = "[]",
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
        RowVersion = Array.Empty<byte>()
    };

    private static PageBuilderEditController CreateController(AppDbContext db) =>
        new(new PageBuilderAdminService(db));

    private static AppDbContext CreateContext() =>
        PageBuilderSqliteTestDatabase.CreateContext();
}
