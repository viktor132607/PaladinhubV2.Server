using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageHistoryControllerTests
{
    [Fact]
    public async Task List_ReturnsAllPagesSortedBySectionAndTitle()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        db.ContentPages.AddRange(
            Page("protection", "zeta", "Zeta"),
            Page("holy", "beta", "Beta"),
            Page("holy", "alpha", "Alpha", isDeleted: true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        PageHistoryController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<PageLifecycleRow>>(result.Value);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "Alpha", "Beta", "Zeta" }, rows.Select(row => row.Title));
        Assert.True(rows[0].IsDeleted);
    }

    [Fact]
    public async Task History_WhenThereAreNoRevisions_ReturnsEmptyList()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        PageHistoryController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(999, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<PageRevision>>(result.Value);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Change_MissingPage_ReturnsNotFoundStatusAndMessage()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        PageHistoryController controller = CreateController(db);

        var result = Assert.IsType<ObjectResult>(await controller.Change(
            999,
            new PageHistoryController.ChangeRequest(1, "archive", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_StaleVersion_ReturnsConflict()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        ContentPage page = Page("holy", "guide", "Guide");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        PageHistoryController controller = CreateController(db);

        var result = Assert.IsType<ObjectResult>(await controller.Change(
            page.Id,
            new PageHistoryController.ChangeRequest(page.Version + 1, "archive", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "This page changed. Refresh before continuing.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_UnknownAction_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        ContentPage page = Page("holy", "guide", "Guide");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        PageHistoryController controller = CreateController(db);

        var result = Assert.IsType<ObjectResult>(await controller.Change(
            page.Id,
            new PageHistoryController.ChangeRequest(page.Version, "unknown", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unknown page action.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_Archive_ReturnsNoContentAndArchivesPage()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        ContentPage page = Page("retribution", "guide", "Guide");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        int version = page.Version;
        PageHistoryController controller = CreateController(db);

        IActionResult result = await controller.Change(
            page.Id,
            new PageHistoryController.ChangeRequest(version, "archive", null),
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(page.IsArchived);
        Assert.False(page.IsPublished);
        Assert.True(page.Version > version);
    }

    private static PageHistoryController CreateController(AppDbContext db)
    {
        var controller = new PageHistoryController(db, new JsonLayoutValidator());
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext());
        return controller;
    }

    private static ContentPage Page(
        string section,
        string slug,
        string title,
        bool isDeleted = false) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = "[]",
        IsPublished = true,
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}
