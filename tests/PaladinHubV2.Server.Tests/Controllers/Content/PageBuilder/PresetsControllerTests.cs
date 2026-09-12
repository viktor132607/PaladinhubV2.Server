using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Presets;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PresetsControllerTests
{
    [Fact]
    public async Task List_ForwardsFiltersAndProjectsRows()
    {
        DateTime updated = DateTime.UtcNow;
        var rows = new List<DataPreset>
        {
            new() { Id = 4, Name = "Raid", Entity = "spells", Section = "holy", UpdatedAt = updated }
        };
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.ListAsync("spells", "holy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.List("spells", "holy", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        object projected = Assert.Single(Assert.IsAssignableFrom<IEnumerable>(ok.Value).Cast<object>());
        Assert.Equal(4, ControllerTestSupport.ReadInt(projected, "Id"));
        Assert.Equal("Raid", ControllerTestSupport.ReadString(projected, "Name"));
        Assert.Equal("spells", ControllerTestSupport.ReadString(projected, "Entity"));
        Assert.Equal("holy", ControllerTestSupport.ReadString(projected, "Section"));
    }

    [Fact]
    public async Task Get_MissingPreset_ReturnsNotFound()
    {
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.GetAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((DataPreset?)null);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Get(7, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ExistingPreset_ReturnsRow()
    {
        var row = new DataPreset { Id = 7, Name = "Preset", Entity = "items" };
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.GetAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Get(7, CancellationToken.None);

        Assert.Same(row, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData("", "items")]
    [InlineData("   ", "items")]
    [InlineData("Preset", "")]
    [InlineData("Preset", "   ")]
    public async Task Create_MissingNameOrEntity_ReturnsBadRequest(string name, string entity)
    {
        var service = new Mock<IDataPresetService>();
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Create(
            new PresetsController.CreateReq(name, entity, "{}", null),
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Name and Entity are required.", ControllerTestSupport.ReadString(bad.Value, "message"));
        service.Verify(
            x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_NullJsonQuery_UsesEmptyObjectAndReturnsCreatedAtGet()
    {
        var created = new DataPreset { Id = 12, Name = "Preset", Entity = "items", JsonQuery = "{}" };
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.CreateAsync("Preset", "items", "{}", "holy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Create(
            new PresetsController.CreateReq("Preset", "items", null!, "holy"),
            CancellationToken.None);

        var response = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(PresetsController.Get), response.ActionName);
        Assert.Equal(12, response.RouteValues!["id"]);
        Assert.Same(created, response.Value);
    }

    [Fact]
    public async Task Update_MissingPreset_ReturnsNotFound()
    {
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.UpdateAsync(5, "Renamed", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataPreset?)null);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Update(
            5,
            new PresetsController.UpdateReq("Renamed", null, null),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ExistingPreset_ReturnsUpdatedRow()
    {
        var updated = new DataPreset { Id = 5, Name = "Renamed", Entity = "items" };
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.UpdateAsync(5, "Renamed", "{\"take\":10}", "prot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Update(
            5,
            new PresetsController.UpdateReq("Renamed", "{\"take\":10}", "prot"),
            CancellationToken.None);

        Assert.Same(updated, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_MapsServiceResult(bool deleted)
    {
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.DeleteAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Delete(9, CancellationToken.None);

        if (deleted)
            Assert.IsType<NoContentResult>(result);
        else
            Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Preview_Success_ReturnsCountAndRows()
    {
        IReadOnlyList<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>
        {
            new() { ["id"] = 1 },
            new() { ["id"] = 2 }
        };
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.ResolveAsync(3, 20, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Preview(3, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, ControllerTestSupport.ReadInt(ok.Value, "count"));
        Assert.Same(rows, ControllerTestSupport.Read(ok.Value, "rows"));
    }

    [Fact]
    public async Task Preview_MissingPreset_ReturnsNotFound()
    {
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.ResolveAsync(3, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Preview(3, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Preview_ResolutionFailure_ReturnsBadRequestWithError()
    {
        var service = new Mock<IDataPresetService>();
        service.Setup(x => x.ResolveAsync(3, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad query"));
        var controller = new PresetsController(service.Object);

        IActionResult result = await controller.Preview(3, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Failed to resolve preset", ControllerTestSupport.ReadString(bad.Value, "message"));
        Assert.Equal("bad query", ControllerTestSupport.ReadString(bad.Value, "error"));
    }
}
