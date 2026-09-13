using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class SpellIconImagesControllerTests
{
    [Fact]
    public async Task Image_MissingIcon_ReturnsNotFound()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        IActionResult result = await controller.Image(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Image_ExistingIcon_ReturnsBytesContentTypeAndSecurityHeaders()
    {
        using AppDbContext db = CreateContext();
        Guid id = Guid.NewGuid();
        byte[] bytes = [137, 80, 78, 71, 1, 2, 3, 4];
        db.SpellIcons.Add(new SpellIcon
        {
            Id = id,
            Name = "icon.png",
            ContentType = "image/png",
            Content = bytes,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(userId: null));

        IActionResult result = await controller.Image(id, TestContext.Current.CancellationToken);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(bytes, file.FileContents);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("public,max-age=31536000,immutable", controller.Response.Headers.CacheControl.ToString());
    }

    private static SpellIconImagesController CreateController(AppDbContext db) =>
        new(db, new GameDataAssignmentService(db));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"spell-icon-images-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
