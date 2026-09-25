using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class MediaControllerTests
{
    [Fact]
    public async Task List_EmptyDatabase_ReturnsEmptyPage()
    {
        await using AppDbContext db = CreateContext();
        MediaController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(
            null,
            "active",
            1,
            32,
            TestContext.Current.CancellationToken));

        Assert.Equal(1, ControllerTestSupport.ReadInt(result.Value, "page"));
        Assert.Equal(1, ControllerTestSupport.ReadInt(result.Value, "pages"));
        Assert.Equal(0, ControllerTestSupport.ReadInt(result.Value, "total"));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        Guid id = Guid.NewGuid();
        db.MediaRevisions.AddRange(
            new MediaRevision { MediaId = id, Version = 1, Action = "uploaded", Snapshot = "{}" },
            new MediaRevision { MediaId = id, Version = 3, Action = "updated", Snapshot = "{}" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        MediaController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<MediaRevision>>(result.Value);

        Assert.Equal(new[] { 3, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Edit_BlankName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        MediaController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Edit(
            Guid.NewGuid(),
            new MediaRequest("   ", null, null, false, 1),
            TestContext.Current.CancellationToken));

        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_MissingImage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        MediaController controller = CreateController(db);

        IActionResult result = await controller.Edit(
            Guid.NewGuid(),
            new MediaRequest("Image", null, null, false, 1),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        SpellIcon media = await AddMediaAsync(db, version: 3);
        MediaController controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Edit(
            media.Id,
            new MediaRequest("Image", null, null, false, 2),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "This image changed in another session. Refresh before saving.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ValidRequest_UpdatesImageAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        SpellIcon media = await AddMediaAsync(db);
        MediaController controller = CreateController(db, "media-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(
            media.Id,
            new MediaRequest("  Renamed  ", "  Alt  ", "  Description  ", true, media.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal(media.Id, ControllerTestSupport.Read(result.Value, "Id"));
        Assert.Equal("Renamed", media.Name);
        Assert.Equal("Alt", media.AltText);
        Assert.Equal("Description", media.Description);
        Assert.True(media.IsArchived);
        Assert.Equal(2, media.Version);
        MediaRevision revision = Assert.Single(db.MediaRevisions);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("media-admin", revision.Actor);
    }

    [Fact]
    public async Task Delete_MissingImage_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        MediaController controller = CreateController(db);

        IActionResult result = await controller.Delete(
            Guid.NewGuid(),
            1,
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_StaleVersion_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        SpellIcon media = await AddMediaAsync(db, version: 4);
        MediaController controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Delete(
            media.Id,
            3,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "This image changed in another session. Refresh before saving.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        SpellIcon media = await AddMediaAsync(db, version: 2, isDeleted: true);
        MediaController controller = CreateController(db);

        IActionResult result = await controller.Restore(
            media.Id,
            new RevisionRestoreRequest(Guid.NewGuid(), media.Version),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    private static MediaController CreateController(AppDbContext db, string actor = "user-1")
    {
        var controller = new MediaController(
            new PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaAdminService(
                db,
                new GameDataAssignmentService(db)));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static async Task<SpellIcon> AddMediaAsync(
        AppDbContext db,
        int version = 1,
        bool isDeleted = false)
    {
        var media = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "image.png",
            ContentType = "image/png",
            Content = new byte[] { 1 },
            Version = version,
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return media;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"media-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
