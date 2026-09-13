using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.API.Controllers.Content.Paladin;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Domain.Services.TalentTrees;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.Paladin;

public sealed class PaladinControllerTests
{
    [Fact]
    public void Index_ReturnsMerchandiseRedirectContract()
    {
        var fixture = CreateFixture();

        IActionResult result = fixture.Controller.Index();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("/Merchandise", ControllerTestSupport.ReadString(ok.Value, "redirectUrl"));
    }

    [Fact]
    public async Task Overview_NormalizesSectionAndRemembersIt()
    {
        var fixture = CreateFixture();

        IActionResult result = await fixture.Controller.Overview(" HOLY ");

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<CombinedViewModel>(ok.Value);
        Assert.Equal("Holy", model.Section);
        Assert.Contains("Holy Paladin", model.PageTitle);
        Assert.Equal("holy", fixture.Session.GetString("current-section"));
        fixture.Spells.Verify(x => x.GetAllAsync(), Times.Once);
        fixture.Items.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [Theory]
    [InlineData("prot", "Protection")]
    [InlineData("ret", "Retribution")]
    public async Task Stats_AcceptsSectionAliases(string routeSection, string expectedSection)
    {
        var fixture = CreateFixture();

        IActionResult result = await fixture.Controller.Stats(routeSection);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<CombinedViewModel>(ok.Value);
        Assert.Equal(expectedSection, model.Section);
        Assert.Contains("Stat Priority", model.PageTitle);
        Assert.Equal(expectedSection.ToLowerInvariant(), fixture.Session.GetString("current-section"));
    }

    [Fact]
    public async Task Talents_BuildsTalentTreesAndRemembersSection()
    {
        var fixture = CreateFixture();
        fixture.TalentTrees
            .Setup(x => x.GetTalentTrees("protection", It.IsAny<List<Spell>>()))
            .ReturnsAsync(new Dictionary<string, TalentTreeViewModel>
            {
                ["protection"] = new() { Key = "protection", Title = "Protection" }
            });

        IActionResult result = await fixture.Controller.Talents("protection");

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<CombinedViewModel>(ok.Value);
        Assert.True(model.TalentTrees.ContainsKey("protection"));
        Assert.Equal("protection", fixture.Session.GetString("current-section"));
        fixture.TalentTrees.Verify(
            x => x.GetTalentTrees("protection", It.Is<List<Spell>>(spells => spells.Count == 1)),
            Times.Once);
    }

    [Fact]
    public async Task Gear_UnsupportedSection_ThrowsBeforeLoadingContent()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Controller.Gear("mage"));

        fixture.Spells.Verify(x => x.GetAllAsync(), Times.Never);
        fixture.Items.Verify(x => x.GetAllAsync(), Times.Never);
    }

    private static PaladinFixture CreateFixture()
    {
        var spells = new Mock<ISpellbookService>();
        spells.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Spell>
        {
            new() { Id = 1, Name = "Holy Shock" }
        });

        var items = new Mock<IItemsService>();
        items.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Item>
        {
            new() { Id = 1, Name = "Shield" }
        });

        var talentTrees = new Mock<ITalentTreeService>();
        talentTrees
            .Setup(x => x.GetTalentTrees(It.IsAny<string>(), It.IsAny<List<Spell>>()))
            .ReturnsAsync(new Dictionary<string, TalentTreeViewModel>());

        var content = new PaladinContentService(
            spells.Object,
            items.Object,
            new HolySectionService(),
            new ProtectionSectionService(),
            new RetributionSectionService(),
            Mock.Of<IPageService>(),
            talentTrees.Object,
            Mock.Of<IBlockRenderer>());

        var controller = new PaladinController(content);
        var session = new TestSession();
        ControllerTestSupport.Attach(
            controller,
            ControllerTestSupport.CreateHttpContext(session: session));

        return new PaladinFixture(controller, session, spells, items, talentTrees);
    }

    private sealed record PaladinFixture(
        PaladinController Controller,
        TestSession Session,
        Mock<ISpellbookService> Spells,
        Mock<IItemsService> Items,
        Mock<ITalentTreeService> TalentTrees);
}
