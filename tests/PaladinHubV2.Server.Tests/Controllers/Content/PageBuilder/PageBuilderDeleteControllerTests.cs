using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageBuilderDeleteControllerTests
{
    [Fact]
    public async Task DeleteConfirm_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).DeleteConfirm("Holy", "missing"));
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task DeleteConfirm_FoundPage_ReturnsViewModel()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("protection", "tank", "Tank Page");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).DeleteConfirm(" Prot ", " TANK "));
        var model = Assert.IsType<DeletePageViewModel>(result.Value);

        Assert.Equal(page.Id, model.Id);
        Assert.Equal("Protection", model.Section);
        Assert.Equal("tank", model.Slug);
        Assert.Equal("Tank Page", model.Title);
        Assert.Equal(page.CreatedAt, model.CreatedAt);
    }

    [Fact]
    public async Task Delete_GetAlias_UsesDeleteConfirmBehavior()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "guide", "Guide");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).Delete("Holy", "guide"));
        Assert.IsType<DeletePageViewModel>(result.Value);
    }

    [Fact]
    public async Task DeleteApi_MissingPage_IsIdempotentNoContent()
    {
        await using AppDbContext db = CreateContext();

        IActionResult result = await CreateController(db).DeleteApi("Holy", "missing");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.ContentPages);
    }

    [Fact]
    public async Task DeleteApi_ExistingPage_RemovesIt()
    {
        await using AppDbContext db = CreateContext();
        db.ContentPages.Add(Page("retribution", "damage", "Damage"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IActionResult result = await CreateController(db).DeleteApi(" Retri ", " DAMAGE ");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.ContentPages.IgnoreQueryFilters());
    }

    [Fact]
    public async Task DeleteConfirmed_FormModel_RemovesMatchingPage()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("protection", "defense", "Defense");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var model = new DeletePageViewModel
        {
            Id = page.Id,
            Section = "Protection",
            Slug = "defense",
            Title = page.Title
        };

        IActionResult result = await CreateController(db).DeleteConfirmed(model);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.ContentPages.IgnoreQueryFilters());
    }

    private static ContentPage Page(string section, string slug, string title) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = "[]",
        IsPublished = true,
        CreatedAt = DateTime.UtcNow.AddHours(-1),
        UpdatedAt = DateTime.UtcNow.AddHours(-1),
        RowVersion = Array.Empty<byte>()
    };

    private static PageBuilderDeleteController CreateController(AppDbContext db) =>
        new(new PageBuilderAdminService(db));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"page-builder-delete-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
