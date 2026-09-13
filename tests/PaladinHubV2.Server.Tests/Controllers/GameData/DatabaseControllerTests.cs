using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class DatabaseControllerTests
{
    [Fact]
    public async Task Index_Items_NormalizesEntityBlankSearchAndPaging()
    {
        using AppDbContext db = CreateContext();
        var controller = new DatabaseController(db);

        IActionResult result = await controller.Index(
            entity: " items ",
            search: "   ",
            page: 0,
            pageSize: 500,
            cancellationToken: TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<AdminDatabaseIndexViewModel>(ok.Value);
        Assert.Equal(AdminEntity.Items, model.Entity);
        Assert.Equal(string.Empty, model.Search);
        Assert.Equal(1, model.Page);
        Assert.Equal(100, model.PageSize);
        Assert.NotNull(model.Items);
        Assert.Null(model.Spells);
        Assert.Empty(model.Items!);
    }

    [Fact]
    public async Task Index_UnknownEntity_DefaultsToSpells()
    {
        using AppDbContext db = CreateContext();
        var controller = new DatabaseController(db);

        IActionResult result = await controller.Index(
            entity: "unknown",
            pageSize: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<AdminDatabaseIndexViewModel>(ok.Value);
        Assert.Equal(AdminEntity.Spells, model.Entity);
        Assert.Equal(1, model.PageSize);
        Assert.NotNull(model.Spells);
        Assert.Null(model.Items);
    }

    [Fact]
    public async Task Index_MissingCategory_ReturnsBadRequestContract()
    {
        using AppDbContext db = CreateContext();
        var controller = new DatabaseController(db);

        IActionResult result = await controller.Index(
            categoryId: 999,
            cancellationToken: TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Category does not exist.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task Index_MissingDiscipline_ReturnsBadRequestContract()
    {
        using AppDbContext db = CreateContext();
        var controller = new DatabaseController(db);

        IActionResult result = await controller.Index(
            disciplineId: 999,
            cancellationToken: TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Class or specialization does not exist.",
            ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"database-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
