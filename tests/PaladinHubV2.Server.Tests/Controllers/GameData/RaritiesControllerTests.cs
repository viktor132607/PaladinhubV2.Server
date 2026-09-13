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

public sealed class RaritiesControllerTests
{
    [Fact]
    public async Task List_ReturnsRaritiesSortedBySortOrderThenName()
    {
        await using AppDbContext db = CreateContext();
        db.ItemRarities.AddRange(
            new ItemRarity { Name = "Epic", Color = "#aa00aa", SortOrder = 2 },
            new ItemRarity { Name = "Rare", Color = "#0000ff", SortOrder = 1 },
            new ItemRarity { Name = "Common", Color = "#ffffff", SortOrder = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<RarityListItem>>(result.Value);

        Assert.Equal(new[] { "Common", "Rare", "Epic" }, rows.Select(row => row.Name));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Rare");
        db.RarityRevisions.AddRange(
            new RarityRevision { RarityId = rarity.Id, Version = 1, Action = "created" },
            new RarityRevision { RarityId = rarity.Id, Version = 4, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(rarity.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<RarityRevision>>(result.Value);

        Assert.Equal(new[] { 4, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            Request("   "), TestContext.Current.CancellationToken));

        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsRarityAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "rarity-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new RarityRequest("  Mythic  ", "  Top tier  ", "#ff8800", 8, false, 0),
            TestContext.Current.CancellationToken));
        var rarity = Assert.IsType<ItemRarity>(result.Value);

        Assert.Equal("Mythic", rarity.Name);
        Assert.Equal("Top tier", rarity.Description);
        RarityRevision revision = Assert.Single(db.RarityRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("rarity-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingRarity_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        IActionResult result = await controller.Edit(999, Request("Rare", 1), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Rare", version: 3);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Edit(
            rarity.Id, Request("Rare", 2), TestContext.Current.CancellationToken));

        Assert.Equal(
            "This rarity changed in another session. Refresh before saving.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_DuplicateName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        await AddRarityAsync(db, "Taken");
        ItemRarity rarity = await AddRarityAsync(db, "Editable");
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Edit(
            rarity.Id, Request(" taken ", rarity.Version), TestContext.Current.CancellationToken));

        Assert.Equal("A rarity with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_StaleVersion_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Rare", version: 2);
        var controller = CreateController(db);

        IActionResult result = await controller.Delete(rarity.Id, 1, TestContext.Current.CancellationToken);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_InUseRarity_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Used");
        db.Items.Add(new Item { Name = "Item", RarityId = rarity.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Delete(
            rarity.Id, rarity.Version, TestContext.Current.CancellationToken));

        Assert.Contains("Remove this rarity", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_UnusedRarity_ReturnsNoContentAndMarksDeleted()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Unused");
        var controller = CreateController(db, "deleter");

        IActionResult result = await controller.Delete(rarity.Id, rarity.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(rarity.IsDeleted);
        Assert.Equal("deleted", Assert.Single(db.RarityRevisions).Action);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Deleted", version: 2, isDeleted: true);
        var controller = CreateController(db);

        IActionResult result = await controller.Restore(
            rarity.Id,
            new RevisionRestoreRequest(Guid.NewGuid(), rarity.Version),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        ItemRarity rarity = await AddRarityAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new ItemRarity
        {
            Id = rarity.Id,
            Name = "Deleted",
            Color = "#ffffff",
            IsDeleted = true,
            Version = 2
        };
        var revision = new RarityRevision
        {
            RarityId = rarity.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.RarityRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Restore(
            rarity.Id,
            new RevisionRestoreRequest(revision.Id, rarity.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_DuplicateHistoricalName_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        await AddRarityAsync(db, "Taken");
        ItemRarity rarity = await AddRarityAsync(db, "Deleted", version: 2, isDeleted: true);
        var snapshot = new ItemRarity
        {
            Id = rarity.Id,
            Name = "Taken",
            Description = "old",
            Color = "#123456",
            Version = 1
        };
        var revision = new RarityRevision
        {
            RarityId = rarity.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.RarityRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Restore(
            rarity.Id,
            new RevisionRestoreRequest(revision.Id, rarity.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("A rarity with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    private static RaritiesController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new RaritiesController(db, new GameDataAssignmentService(db));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static RarityRequest Request(string name, int version = 1) =>
        new(name, string.Empty, "#abcdef", 0, false, version);

    private static async Task<ItemRarity> AddRarityAsync(
        AppDbContext db,
        string name,
        int version = 1,
        bool isDeleted = false)
    {
        var rarity = new ItemRarity
        {
            Name = name,
            Color = "#abcdef",
            Version = version,
            IsDeleted = isDeleted,
            IsArchived = isDeleted
        };
        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return rarity;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rarities-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
