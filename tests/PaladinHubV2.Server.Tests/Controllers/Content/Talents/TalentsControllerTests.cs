using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.API.Controllers.Content.Talents;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.TalentTrees;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.Talents;

public sealed class TalentsControllerTests
{
    [Fact]
    public async Task SectionPage_InvalidSection_ReturnsBadRequestWithoutLoadingTrees()
    {
        using AppDbContext db = CreateContext();
        var trees = new Mock<ITalentTreeService>();
        var controller = new TalentsController(db, trees.Object);

        IActionResult result = await controller.SectionPage("mage");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid paladin section.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
        trees.Verify(x => x.GetTalentTrees(It.IsAny<string>(), It.IsAny<List<Spell>>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_InvalidSection_ReturnsSpecificBadRequest()
    {
        using AppDbContext db = CreateContext();
        var trees = new Mock<ITalentTreeService>();
        var controller = new TalentsController(db, trees.Object);

        IActionResult result = await controller.GetAll("warrior");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Section must be holy, protection or retribution.",
            ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task GetTree_BlankKey_ReturnsBadRequest()
    {
        using AppDbContext db = CreateContext();
        var controller = new TalentsController(db, Mock.Of<ITalentTreeService>());

        IActionResult result = await controller.GetTree("   ", "holy");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Talent tree key is required.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task GetTree_UnresolvableSection_ReturnsBadRequest()
    {
        using AppDbContext db = CreateContext();
        var controller = new TalentsController(db, Mock.Of<ITalentTreeService>());

        IActionResult result = await controller.GetTree("templar", null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Section could not be resolved. Pass ?section=holy, protection or retribution.",
            ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task GetAll_NormalizesAliasAndReturnsOrderedSectionKeys()
    {
        using AppDbContext db = CreateContext();
        db.Spells.Add(new Spell { Id = 1, Name = "Shield of the Righteous" });
        db.Items.Add(new Item { Id = 1, Name = "Bulwark" });
        await db.SaveChangesAsync();
        var trees = CreateTreeService("protection");
        var controller = new TalentsController(db, trees.Object);

        IActionResult result = await controller.GetAll("prot");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("protection", ControllerTestSupport.ReadString(ok.Value, "section"));
        var keys = Assert.IsType<string[]>(ControllerTestSupport.Read(ok.Value, "keys"));
        Assert.Equal(new[] { "paladin", "protection-templar", "protection" }, keys);
        Assert.Equal("Protection Paladin – Talents", ControllerTestSupport.ReadString(ok.Value, "pageTitle"));
        trees.Verify(
            x => x.GetTalentTrees("protection", It.Is<List<Spell>>(spells => spells.Count == 1)),
            Times.Once);
    }

    [Fact]
    public async Task GetTree_ResolvesSectionFromKeyAndReturnsSelectedTrees()
    {
        using AppDbContext db = CreateContext();
        var trees = CreateTreeService("protection");
        var controller = new TalentsController(db, trees.Object);

        IActionResult result = await controller.GetTree("protection-templar", null);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("protection", ControllerTestSupport.ReadString(ok.Value, "section"));
        Assert.Equal("protection-templar", ControllerTestSupport.ReadString(ok.Value, "requestedKey"));
        Assert.Equal("protection-templar", ControllerTestSupport.ReadString(ok.Value, "resolvedKey"));
        var keys = Assert.IsType<string[]>(ControllerTestSupport.Read(ok.Value, "keys"));
        Assert.Equal(new[] { "paladin", "protection-templar", "protection" }, keys);
    }

    [Fact]
    public async Task GetTree_UnknownTree_ReturnsNotFoundWithResolvedSection()
    {
        using AppDbContext db = CreateContext();
        var trees = CreateTreeService("holy");
        var controller = new TalentsController(db, trees.Object);

        IActionResult result = await controller.GetTree("unknown", "holy");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(
            "No talent tree was found for key 'unknown' in section 'holy'.",
            ControllerTestSupport.ReadString(notFound.Value, "message"));
    }

    private static Mock<ITalentTreeService> CreateTreeService(string section)
    {
        var service = new Mock<ITalentTreeService>();
        var data = new Dictionary<string, TalentTreeViewModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["paladin"] = new() { Key = "paladin", Title = "Paladin" },
            [section] = new() { Key = section, Title = section },
            [$"{section}-templar"] = new() { Key = $"{section}-templar", Title = "Templar", IsHero = true }
        };
        service.Setup(x => x.GetTalentTrees(section, It.IsAny<List<Spell>>()))
            .ReturnsAsync(data);
        return service;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"talents-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
