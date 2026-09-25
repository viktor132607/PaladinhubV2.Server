using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class TagsControllerTests
{
    [Fact]
    public async Task List_ReturnsTagsSortedBySortOrderThenName()
    {
        await using AppDbContext db = CreateContext();
        db.GameTags.AddRange(
            new GameTag { Name = "PvP", SortOrder = 2 },
            new GameTag { Name = "Healing", SortOrder = 1 },
            new GameTag { Name = "Damage", SortOrder = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<TagListItem>>(result.Value);

        Assert.Equal(new[] { "Damage", "Healing", "PvP" }, rows.Select(row => row.Name));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Tag");
        db.TagRevisions.AddRange(
            new TagRevision { TagId = tag.Id, Version = 1, Action = "created" },
            new TagRevision { TagId = tag.Id, Version = 3, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(tag.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<TagRevision>>(result.Value);

        Assert.Equal(new[] { 3, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            Request("  "), TestContext.Current.CancellationToken));

        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "tag-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new TagRequest("  Burst  ", "  High damage  ", 4, false, 0),
            TestContext.Current.CancellationToken));
        var tag = Assert.IsType<GameTag>(result.Value);

        Assert.Equal("Burst", tag.Name);
        Assert.Equal("High damage", tag.Description);
        TagRevision revision = Assert.Single(db.TagRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("tag-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingTag_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        IActionResult result = await controller.Edit(999, Request("Tag"), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Tag", version: 3);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Edit(
            tag.Id, Request("Tag", 2), TestContext.Current.CancellationToken));

        Assert.Equal(
            "This tag changed in another session. Refresh before saving.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_DuplicateName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        await AddTagAsync(db, "Taken");
        GameTag tag = await AddTagAsync(db, "Editable");
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Edit(
            tag.Id, Request(" taken ", tag.Version), TestContext.Current.CancellationToken));

        Assert.Equal("A tag with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ValidRequest_ReturnsUpdatedTag()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Old");
        var controller = CreateController(db, "editor");

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(
            tag.Id,
            new TagRequest("  New  ", "  Changed  ", 6, true, tag.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<GameTag>(result.Value);

        Assert.Equal("New", updated.Name);
        Assert.Equal("Changed", updated.Description);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        TagRevision revision = Assert.Single(db.TagRevisions);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Delete_InUseTag_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Used");
        db.Items.Add(new Item { Name = "Item", TagIds = [tag.Id] });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Delete(
            tag.Id, tag.Version, TestContext.Current.CancellationToken));

        Assert.Contains("Remove this tag", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_ValidTag_ReturnsNoContentAndMarksDeleted()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Unused");
        var controller = CreateController(db, "deleter");

        IActionResult result = await controller.Delete(tag.Id, tag.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(tag.IsDeleted);
        Assert.Equal("deleted", Assert.Single(db.TagRevisions).Action);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Deleted", version: 2, isDeleted: true);
        var controller = CreateController(db);

        IActionResult result = await controller.Restore(
            tag.Id,
            new RevisionRestoreRequest(Guid.NewGuid(), tag.Version),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GameTag { Id = tag.Id, Name = "Deleted", IsDeleted = true, Version = 2 };
        var revision = new TagRevision
        {
            TagId = tag.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.TagRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Restore(
            tag.Id,
            new RevisionRestoreRequest(revision.Id, tag.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_DuplicateHistoricalName_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        await AddTagAsync(db, "Taken");
        GameTag tag = await AddTagAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GameTag { Id = tag.Id, Name = "Taken", Description = "old", Version = 1 };
        var revision = new TagRevision
        {
            TagId = tag.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.TagRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Restore(
            tag.Id,
            new RevisionRestoreRequest(revision.Id, tag.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("A tag with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_ValidRevision_ReturnsRestoredTag()
    {
        await using AppDbContext db = CreateContext();
        GameTag tag = await AddTagAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GameTag
        {
            Id = tag.Id,
            Name = "Recovered",
            Description = "Historical",
            SortOrder = 5,
            Version = 1
        };
        var revision = new TagRevision
        {
            TagId = tag.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.TagRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "restorer");

        var result = Assert.IsType<OkObjectResult>(await controller.Restore(
            tag.Id,
            new RevisionRestoreRequest(revision.Id, tag.Version),
            TestContext.Current.CancellationToken));
        var restored = Assert.IsType<GameTag>(result.Value);

        Assert.False(restored.IsDeleted);
        Assert.Equal("Recovered", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.TagRevisions, value => value.Action == "restored" && value.Actor == "restorer");
    }

    private static TagsController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new TagsController(new TagAdminService(db, new GameDataAssignmentService(db)));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static TagRequest Request(string name, int version = 1) =>
        new(name, string.Empty, 0, false, version);

    private static async Task<GameTag> AddTagAsync(
        AppDbContext db,
        string name,
        int version = 1,
        bool isDeleted = false)
    {
        var tag = new GameTag
        {
            Name = name,
            Version = version,
            IsDeleted = isDeleted,
            IsArchived = isDeleted
        };
        db.GameTags.Add(tag);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tag;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tags-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
