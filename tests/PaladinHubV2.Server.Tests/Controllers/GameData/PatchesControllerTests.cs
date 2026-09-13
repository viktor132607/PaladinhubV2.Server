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

public sealed class PatchesControllerTests
{
    [Fact]
    public async Task List_ReturnsPatchesSortedBySortOrderThenName()
    {
        await using AppDbContext db = CreateContext();
        db.GamePatches.AddRange(
            new GamePatch { Name = "Zulu", SortOrder = 2 },
            new GamePatch { Name = "Beta", SortOrder = 1 },
            new GamePatch { Name = "Alpha", SortOrder = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<PatchListItem>>(result.Value);

        Assert.Equal(new[] { "Alpha", "Beta", "Zulu" }, rows.Select(row => row.Name));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Patch");
        db.PatchRevisions.AddRange(
            new PatchRevision { PatchId = patch.Id, Version = 1, Action = "created" },
            new PatchRevision { PatchId = patch.Id, Version = 3, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(patch.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<PatchRevision>>(result.Value);

        Assert.Equal(new[] { 3, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            new PatchRequest("   ", null, 0, false, 0),
            TestContext.Current.CancellationToken));

        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsValuesAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "patch-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new PatchRequest("  Patch 11.0  ", "  Launch patch  ", 7, false, 0),
            TestContext.Current.CancellationToken));
        var patch = Assert.IsType<GamePatch>(result.Value);

        Assert.Equal("Patch 11.0", patch.Name);
        Assert.Equal("Launch patch", patch.Description);
        PatchRevision revision = Assert.Single(db.PatchRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("patch-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingPatch_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        IActionResult result = await controller.Edit(999,
            new PatchRequest("Patch", null, 0, false, 1),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Patch", version: 3);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Edit(
            patch.Id,
            new PatchRequest("Patch", null, 0, false, 2),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "This patch changed in another session. Refresh before saving.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_DuplicateName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        await AddPatchAsync(db, "Taken");
        GamePatch patch = await AddPatchAsync(db, "Editable");
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Edit(
            patch.Id,
            new PatchRequest(" taken ", null, 0, false, patch.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("A patch with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ValidRequest_ReturnsUpdatedPatch()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Old");
        var controller = CreateController(db, "editor");

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(
            patch.Id,
            new PatchRequest("  New  ", "  Changed  ", 4, true, patch.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<GamePatch>(result.Value);

        Assert.Equal("New", updated.Name);
        Assert.Equal("Changed", updated.Description);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        PatchRevision revision = Assert.Single(db.PatchRevisions);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Delete_InUsePatch_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Used");
        db.Items.Add(new Item { Name = "Item", PatchId = patch.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Delete(
            patch.Id, patch.Version, TestContext.Current.CancellationToken));

        Assert.Contains("Remove this patch", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_ValidPatch_ReturnsNoContentAndMarksDeleted()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Unused");
        var controller = CreateController(db, "deleter");

        IActionResult result = await controller.Delete(patch.Id, patch.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(patch.IsDeleted);
        Assert.Equal(2, patch.Version);
        Assert.Equal("deleted", Assert.Single(db.PatchRevisions).Action);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Deleted", version: 2, isDeleted: true);
        var controller = CreateController(db);

        IActionResult result = await controller.Restore(
            patch.Id,
            new RevisionRestoreRequest(Guid.NewGuid(), patch.Version),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GamePatch { Id = patch.Id, Name = "Deleted", IsDeleted = true, Version = 2 };
        var revision = new PatchRevision
        {
            PatchId = patch.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.PatchRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Restore(
            patch.Id,
            new RevisionRestoreRequest(revision.Id, patch.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_DuplicateHistoricalName_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        await AddPatchAsync(db, "Taken");
        GamePatch patch = await AddPatchAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GamePatch { Id = patch.Id, Name = "Taken", Description = "old", Version = 1 };
        var revision = new PatchRevision
        {
            PatchId = patch.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.PatchRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Restore(
            patch.Id,
            new RevisionRestoreRequest(revision.Id, patch.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("A patch with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_ValidRevision_ReturnsRestoredPatch()
    {
        await using AppDbContext db = CreateContext();
        GamePatch patch = await AddPatchAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new GamePatch
        {
            Id = patch.Id,
            Name = "Recovered",
            Description = "Historical",
            SortOrder = 5,
            Version = 1
        };
        var revision = new PatchRevision
        {
            PatchId = patch.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.PatchRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "restorer");

        var result = Assert.IsType<OkObjectResult>(await controller.Restore(
            patch.Id,
            new RevisionRestoreRequest(revision.Id, patch.Version),
            TestContext.Current.CancellationToken));
        var restored = Assert.IsType<GamePatch>(result.Value);

        Assert.False(restored.IsDeleted);
        Assert.Equal("Recovered", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.PatchRevisions, value => value.Action == "restored" && value.Actor == "restorer");
    }

    private static PatchesController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new PatchesController(db, new GameDataAssignmentService(db));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static async Task<GamePatch> AddPatchAsync(
        AppDbContext db,
        string name,
        int version = 1,
        bool isDeleted = false)
    {
        var patch = new GamePatch
        {
            Name = name,
            Description = string.Empty,
            Version = version,
            IsDeleted = isDeleted,
            IsArchived = isDeleted
        };
        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return patch;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patches-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
