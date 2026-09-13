using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class SpellIconsControllerTests
{
    [Fact]
    public async Task Browse_EmptyLibrary_ReturnsEmptyPage()
    {
        await using AppDbContext db = CreateContext();
        SpellIconsController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.Browse(
            null,
            1,
            64,
            TestContext.Current.CancellationToken));

        Assert.Equal(1, ControllerTestSupport.ReadInt(result.Value, "page"));
        Assert.Equal(1, ControllerTestSupport.ReadInt(result.Value, "pages"));
        Assert.Equal(0, ControllerTestSupport.ReadInt(result.Value, "total"));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(ControllerTestSupport.Read(result.Value, "icons")));
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        SpellIconsController controller = CreateController(db);
        IFormFile file = File(Array.Empty<byte>(), "empty.png");

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Upload(
            file,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "Choose a PNG, JPEG, GIF or WebP image up to 5 MB.",
            ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Empty(db.SpellIcons);
    }

    [Fact]
    public async Task Upload_InvalidImageType_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        SpellIconsController controller = CreateController(db);
        IFormFile file = File(new byte[] { 1, 2, 3, 4 }, "fake.png");

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Upload(
            file,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "Only PNG, JPEG, GIF and WebP images are supported.",
            ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Empty(db.SpellIcons);
    }

    [Fact]
    public async Task Upload_ValidPng_PersistsImageAndAuditActor()
    {
        await using AppDbContext db = CreateContext();
        SpellIconsController controller = CreateController(db, "icon-admin");
        byte[] png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        IFormFile file = File(png, "holy.png");

        var result = Assert.IsType<OkObjectResult>(await controller.Upload(
            file,
            TestContext.Current.CancellationToken));

        SpellIcon icon = Assert.Single(db.SpellIcons);
        Assert.Equal("holy.png", icon.Name);
        Assert.Equal("image/png", icon.ContentType);
        Assert.Equal("holy.png", ControllerTestSupport.ReadString(result.Value, "name"));
        Assert.Equal($"/api/spell-icons/{icon.Id}", ControllerTestSupport.ReadString(result.Value, "icon"));
        MediaRevision revision = Assert.Single(db.MediaRevisions);
        Assert.Equal("uploaded", revision.Action);
        Assert.Equal("icon-admin", revision.Actor);
    }

    private static SpellIconsController CreateController(AppDbContext db, string userId = "user-1")
    {
        var controller = new SpellIconsController(db, new GameDataAssignmentService(db));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(userId));
        return controller;
    }

    private static IFormFile File(byte[] content, string name) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"spell-icons-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
