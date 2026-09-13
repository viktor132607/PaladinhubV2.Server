using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class RecordTypesControllerTests
{
    [Fact]
    public async Task List_ReturnsTypesSortedByName()
    {
        await using AppDbContext db = CreateContext();
        db.RecordTypes.AddRange(
            new RecordType { Name = "zeta" },
            new RecordType { Name = "alpha" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new RecordTypesController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<RecordTypeListItem>>(result.Value);

        Assert.Equal(new[] { "alpha", "zeta" }, rows.Select(row => row.Name));
        Assert.All(rows, row => Assert.Equal(0, row.UsageCount));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var controller = new RecordTypesController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            new RecordTypesController.TypeRequest("   "),
            TestContext.Current.CancellationToken));

        Assert.Equal("Type must contain 1–50 characters.", ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Empty(db.RecordTypes);
    }

    [Fact]
    public async Task Create_ValidName_NormalizesAndPersistsType()
    {
        await using AppDbContext db = CreateContext();
        var controller = new RecordTypesController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new RecordTypesController.TypeRequest("  Aura  "),
            TestContext.Current.CancellationToken));

        Assert.Equal("aura", ControllerTestSupport.ReadString(result.Value, "name"));
        Assert.Equal(0, ControllerTestSupport.ReadInt(result.Value, "usageCount"));
        Assert.Equal("aura", Assert.Single(db.RecordTypes).Name);
    }

    [Fact]
    public async Task Rename_BlankTargetName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var controller = new RecordTypesController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Rename(
            "spell",
            new RecordTypesController.TypeRequest(" "),
            TestContext.Current.CancellationToken));

        Assert.Equal("Type must contain 1–50 characters.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_SameReplacement_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var controller = new RecordTypesController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Delete(
            "spell",
            "spell",
            TestContext.Current.CancellationToken));

        Assert.Equal("Choose a different replacement type.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"record-types-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
