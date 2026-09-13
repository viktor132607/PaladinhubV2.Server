using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class CategoriesControllerTests
{
    [Fact]
    public async Task List_ReturnsSortedRowsWithChildCounts()
    {
        await using AppDbContext db = CreateContext();
        Category beta = await AddAsync(db, "Beta", sortOrder: 1);
        await AddAsync(db, "Child", parentId: beta.Id, sortOrder: 0);
        await AddAsync(db, "Zulu", sortOrder: 2);
        await AddAsync(db, "Alpha", sortOrder: 1);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<CategoryListItem>>(result.Value);

        Assert.Equal(new[] { "Child", "Alpha", "Beta", "Zulu" }, rows.Select(row => row.Name));
        Assert.Equal(1, rows.Single(row => row.Id == beta.Id).ChildCount);
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Category");
        db.CategoryRevisions.AddRange(
            new CategoryRevision { CategoryId = category.Id, Version = 1, Action = "created" },
            new CategoryRevision { CategoryId = category.Id, Version = 4, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(category.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<CategoryRevision>>(result.Value);

        Assert.Equal(new[] { 4, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("   "), TestContext.Current.CancellationToken));
        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateSiblingName_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        Category parent = await AddAsync(db, "Parent");
        await AddAsync(db, "Taken", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request(" taken ", parent.Id), TestContext.Current.CancellationToken));

        Assert.Equal("A category with this name already exists under this parent.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_MissingParent_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Child", 999), TestContext.Current.CancellationToken));
        Assert.Equal("Parent category does not exist. Restore it first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ActiveChildOfArchivedParent_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        Category parent = await AddAsync(db, "Archived", isArchived: true);
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Child", parent.Id), TestContext.Current.CancellationToken));
        Assert.Equal("An active category cannot have an archived parent.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsValuesAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "category-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new CategoryRequest("  Consumables  ", "  Useful things  ", null, 8, false, 0),
            TestContext.Current.CancellationToken));
        var category = Assert.IsType<Category>(result.Value);

        Assert.Equal("Consumables", category.Name);
        Assert.Equal("Useful things", category.Description);
        CategoryRevision revision = Assert.Single(db.CategoryRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("category-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingCategory_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        IActionResult result = await CreateController(db).Edit(999, Request("Missing", version: 1), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Category", version: 3);
        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Edit(
            category.Id, Request("Category", version: 2), TestContext.Current.CancellationToken));
        Assert.Equal("This category changed in another session. Refresh before saving.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_CannotMoveCategoryInsideItself()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Category");
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            category.Id, Request("Category", category.Id, version: category.Version), TestContext.Current.CancellationToken));
        Assert.Equal("A category cannot be placed inside itself or its descendants.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_CannotArchiveParentWithActiveChild()
    {
        await using AppDbContext db = CreateContext();
        Category parent = await AddAsync(db, "Parent");
        await AddAsync(db, "Child", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            parent.Id,
            new CategoryRequest(parent.Name, null, null, 0, true, parent.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Archive or move the active subcategories first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ValidRequest_ReturnsUpdatedCategoryAndArchiveRevision()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Old");
        var controller = CreateController(db, "editor");

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(
            category.Id,
            new CategoryRequest("  New  ", "  Changed  ", null, 4, true, category.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<Category>(result.Value);

        Assert.Equal("New", updated.Name);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        CategoryRevision revision = Assert.Single(db.CategoryRevisions);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Delete_CategoryWithChild_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        Category parent = await AddAsync(db, "Parent");
        await AddAsync(db, "Child", parentId: parent.Id);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Delete(
            parent.Id, parent.Version, TestContext.Current.CancellationToken));

        Assert.Contains("Move the subcategories", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_StaleVersion_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Category", version: 3);
        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Delete(
            category.Id, 2, TestContext.Current.CancellationToken));
        Assert.Equal("This category changed in another session. Refresh before saving.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_UnusedCategory_ReturnsNoContentAndMarksDeleted()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Unused");

        IActionResult result = await CreateController(db, "deleter").Delete(
            category.Id, category.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(category.IsDeleted);
        Assert.Equal(2, category.Version);
        CategoryRevision revision = Assert.Single(db.CategoryRevisions);
        Assert.Equal("deleted", revision.Action);
        Assert.Equal("deleter", revision.Actor);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        IActionResult result = await CreateController(db).Restore(
            category.Id, new RevisionRestoreRequest(Guid.NewGuid(), category.Version), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        var revision = new CategoryRevision
        {
            CategoryId = category.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(new Category { Id = category.Id, Name = "Deleted", IsDeleted = true, Version = 2 })
        };
        db.CategoryRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Restore(
            category.Id, new RevisionRestoreRequest(revision.Id, category.Version), TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_ValidRevision_ReturnsRestoredCategory()
    {
        await using AppDbContext db = CreateContext();
        Category category = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        var revision = new CategoryRevision
        {
            CategoryId = category.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(new Category { Id = category.Id, Name = "Recovered", Description = "Historical", Version = 1 })
        };
        db.CategoryRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db, "restorer").Restore(
            category.Id, new RevisionRestoreRequest(revision.Id, category.Version), TestContext.Current.CancellationToken));
        var restored = Assert.IsType<Category>(result.Value);

        Assert.False(restored.IsDeleted);
        Assert.Equal("Recovered", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.CategoryRevisions, value => value.Action == "restored" && value.Actor == "restorer");
    }

    private static CategoryRequest Request(string name, int? parentId = null, int version = 0) =>
        new(name, null, parentId, 0, false, version);

    private static CategoriesController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new CategoriesController(db, new GameDataAssignmentService(db));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static async Task<Category> AddAsync(
        AppDbContext db,
        string name,
        int? parentId = null,
        int sortOrder = 0,
        int version = 1,
        bool isArchived = false,
        bool isDeleted = false)
    {
        var category = new Category
        {
            Name = name,
            Description = string.Empty,
            ParentId = parentId,
            SortOrder = sortOrder,
            Version = version,
            IsArchived = isArchived || isDeleted,
            IsDeleted = isDeleted
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return category;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"categories-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
