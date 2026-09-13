using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class ItemsControllerTests
{
    [Fact]
    public void Create_ReturnsEmptyItemModel()
    {
        using AppDbContext db = CreateContext();
        var controller = new ItemsController(db);

        IActionResult result = controller.Create();

        var ok = Assert.IsType<OkObjectResult>(result);
        var item = Assert.IsType<Item>(ok.Value);
        Assert.Equal(0, item.Id);
        Assert.Equal(string.Empty, item.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Details_InvalidId_ReturnsBadRequest(int id)
    {
        using AppDbContext db = CreateContext();
        var controller = new ItemsController(db);

        IActionResult result = await controller.Details(id, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid item ID.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task Edit_MissingItem_ReturnsNotFound()
    {
        using AppDbContext db = CreateContext();
        var controller = new ItemsController(db);

        IActionResult result = await controller.Edit(99, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Item not found.", ControllerTestSupport.ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task Details_ExistingItem_ReturnsItem()
    {
        using AppDbContext db = CreateContext();
        db.Items.Add(new Item { Id = 7, Name = "Dawnstone" });
        await db.SaveChangesAsync();
        var controller = new ItemsController(db);

        IActionResult result = await controller.Details(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var item = Assert.IsType<Item>(ok.Value);
        Assert.Equal(7, item.Id);
        Assert.Equal("Dawnstone", item.Name);
    }

    [Fact]
    public async Task Delete_ExistingItem_ReturnsItemWithoutDeletingIt()
    {
        using AppDbContext db = CreateContext();
        db.Items.Add(new Item { Id = 11, Name = "Sun Shard" });
        await db.SaveChangesAsync();
        var controller = new ItemsController(db);

        IActionResult result = await controller.Delete(11, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(11, Assert.IsType<Item>(ok.Value).Id);
        Assert.NotNull(await db.Items.FindAsync(11));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"items-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
