using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageManagementMutationsControllerTests
{
    [Fact]
    public async Task Create_NullRequest_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            null!, TestContext.Current.CancellationToken));
        Assert.Equal("Request body is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidSection_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(section: "shadow"), "Section must be Holy, Protection, or Retribution.");
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(title: "   "), "Page title is required.");
    }

    [Fact]
    public async Task Create_TitleTooLong_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(title: new string('x', 201)), "Page title cannot exceed 200 characters.");
    }

    [Fact]
    public async Task Create_BlankSlug_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(slug: "   "), "Slug is required.");
    }

    [Fact]
    public async Task Create_SlugWithoutLettersOrNumbers_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(slug: "--- !!! ---"), "Slug must contain letters or numbers.");
    }

    [Fact]
    public async Task Create_SlugTooLong_ReturnsBadRequest()
    {
        await AssertCreateValidationAsync(Request(slug: new string('a', 101)), "Slug cannot exceed 100 characters.");
    }

    [Fact]
    public async Task Create_ReservedBuiltInRoute_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Create(
            Request(section: "Holy", slug: "overview"), TestContext.Current.CancellationToken));

        Assert.Equal(
            "This route belongs to a hardcoded page and cannot be replaced from Page Builder.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_RecoverableDuplicateSlug_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        db.ContentPages.Add(Page("holy", "custom", "Deleted", isDeleted: true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Create(
            Request(section: "Holy", slug: "custom"), TestContext.Current.CancellationToken));

        Assert.Equal("Slug is already used in this section.", ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Single(db.ContentPages.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_ValidRequest_NormalizesPersistsActorAndReturnsCreated()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "page-admin");

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            Request(section: " Prot ", title: "  Mythic Tank  ", slug: " My Fancy--Page!!! ", isPublished: false),
            TestContext.Current.CancellationToken));
        ContentPage page = Assert.Single(db.ContentPages);

        Assert.Equal("protection", page.Section);
        Assert.Equal("Mythic Tank", page.Title);
        Assert.Equal("my-fancy-page", page.Slug);
        Assert.False(page.IsPublished);
        Assert.Equal("page-admin", page.UpdatedBy);
        Assert.Equal("[]", page.JsonLayout);
        Assert.Equal(nameof(PageManagementController.Get), result.ActionName);
        Assert.Equal("PageManagement", result.ControllerName);
        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal("Protection", ControllerTestSupport.ReadString(result.Value, "section"));
    }

    [Fact]
    public async Task Update_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).Update(
            999, Request(), TestContext.Current.CancellationToken));
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_InvalidRequest_IsRejectedBeforeLookup()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Update(
            999, Request(title: " "), TestContext.Current.CancellationToken));
        Assert.Equal("Page title is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ReservedBuiltInRoute_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "custom", "Custom", rowVersion: [1]);
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Update(
            page.Id,
            Request(section: "Holy", slug: "gear", rowVersion: page.RowVersion),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "This route belongs to a hardcoded page and cannot be replaced from Page Builder.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_DuplicateSlug_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "first", "First", rowVersion: [1]);
        ContentPage other = Page("holy", "taken", "Taken");
        db.ContentPages.AddRange(page, other);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Update(
            page.Id,
            Request(section: "Holy", slug: "taken", rowVersion: page.RowVersion),
            TestContext.Current.CancellationToken));

        Assert.Equal("Slug is already used in this section.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_InvalidRowVersionEncoding_ReturnsConcurrencyConflict()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "custom", "Custom", rowVersion: [1, 2]);
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        SavePageRequest request = Request();
        request = new SavePageRequest
        {
            Section = request.Section,
            Title = request.Title,
            Slug = request.Slug,
            IsPublished = request.IsPublished,
            RowVersionBase64 = "%%%"
        };
        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Update(
            page.Id, request, TestContext.Current.CancellationToken));

        Assert.Equal(
            "The page changed while you were editing it. Reload and try again.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_MismatchedRowVersion_ReturnsConcurrencyConflict()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "custom", "Custom", rowVersion: [1, 2]);
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Update(
            page.Id, Request(rowVersion: [9, 9]), TestContext.Current.CancellationToken));

        Assert.Equal(
            "The page changed while you were editing it. Reload and try again.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesPageAndActor()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "custom", "Old", rowVersion: [1, 2, 3]);
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "editor-9");

        var result = Assert.IsType<OkObjectResult>(await controller.Update(
            page.Id,
            Request(section: " Ret ", title: "  New title  ", slug: " New Route ", isPublished: false, rowVersion: page.RowVersion),
            TestContext.Current.CancellationToken));

        Assert.Equal("retribution", page.Section);
        Assert.Equal("New title", page.Title);
        Assert.Equal("new-route", page.Slug);
        Assert.False(page.IsPublished);
        Assert.Equal("editor-9", page.UpdatedBy);
        Assert.Equal("Retribution", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("new-route", ControllerTestSupport.ReadString(result.Value, "slug"));
    }

    [Fact]
    public async Task Delete_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).Delete(
            999, TestContext.Current.CancellationToken));
        Assert.Equal("Page not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_ExistingPage_ReturnsNoContentAndRemovesIt()
    {
        await using AppDbContext db = CreateContext();
        ContentPage page = Page("holy", "custom", "Custom");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IActionResult result = await CreateController(db).Delete(page.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.ContentPages.IgnoreQueryFilters());
    }

    private static async Task AssertCreateValidationAsync(SavePageRequest request, string expected)
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            request, TestContext.Current.CancellationToken));
        Assert.Equal(expected, ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Empty(db.ContentPages.IgnoreQueryFilters());
    }

    private static SavePageRequest Request(
        string? section = "Holy",
        string? title = "Custom page",
        string? slug = "custom",
        bool isPublished = true,
        byte[]? rowVersion = null) => new()
    {
        Section = section,
        Title = title,
        Slug = slug,
        IsPublished = isPublished,
        RowVersionBase64 = rowVersion == null ? null : Convert.ToBase64String(rowVersion)
    };

    private static ContentPage Page(
        string section,
        string slug,
        string title,
        bool isDeleted = false,
        byte[]? rowVersion = null) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = "[]",
        IsPublished = true,
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
        RowVersion = rowVersion ?? Array.Empty<byte>()
    };

    private static PageManagementMutationsController CreateController(AppDbContext db, string actor = "admin")
    {
        var controller = new PageManagementMutationsController(new PageManagementService(db));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, actor) },
                "Test"))
        };
        ControllerTestSupport.Attach(controller, context);
        return controller;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"page-management-mutations-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
