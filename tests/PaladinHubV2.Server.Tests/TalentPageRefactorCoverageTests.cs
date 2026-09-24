using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.Tests;

public sealed class TalentPageRefactorCoverageTests
{
    [Fact]
    public void SelectorNormalizesSectionsAndResolvesSources()
    {
        var selector = new TalentPageTreeSelector();

        Assert.Equal("holy", selector.NormalizeSection(" holy "));
        Assert.Equal("holy", selector.NormalizeSection("HOLLY"));
        Assert.Equal("protection", selector.NormalizeSection("prot"));
        Assert.Equal("protection", selector.NormalizeSection("Protection"));
        Assert.Equal("retribution", selector.NormalizeSection("ret"));
        Assert.Equal("retribution", selector.NormalizeSection("retri"));
        Assert.Equal("retribution", selector.NormalizeSection("Retribution"));
        Assert.Null(selector.NormalizeSection(null));
        Assert.Null(selector.NormalizeSection("mage"));

        Assert.Equal(
            "holy",
            selector.ResolveTreeSection(
                "protection-templar",
                "holy"));
        Assert.Equal(
            "protection",
            selector.ResolveTreeSection(
                "protection-templar",
                null));
        Assert.Null(
            selector.ResolveTreeSection(
                "templar",
                null));
    }

    [Fact]
    public void SelectorBuildsSectionKeysAcrossHeroFallbacks()
    {
        var selector = new TalentPageTreeSelector();

        Dictionary<string, TalentTreeViewModel> heraldTrees =
            Trees(
                "paladin",
                "holy",
                "holy-herald");

        Assert.Equal(
            ["paladin", "holy-herald", "holy"],
            selector.BuildSectionKeys(
                heraldTrees,
                "holy"));

        Dictionary<string, TalentTreeViewModel> lightsmithTrees =
            Trees(
                "paladin",
                "holy",
                "holy-lightsmith");

        Assert.Equal(
            ["paladin", "holy-lightsmith", "holy"],
            selector.BuildSectionKeys(
                lightsmithTrees,
                "holy"));

        Dictionary<string, TalentTreeViewModel> templarTrees =
            Trees(
                "PALADIN",
                "holy-herald",
                "holy-lightsmith",
                "holy-templar",
                "holy-spec");

        Assert.Equal(
            ["PALADIN", "holy-herald", "holy-spec"],
            selector.BuildSectionKeys(
                templarTrees,
                "holy"));

        Dictionary<string, TalentTreeViewModel> globalTemplar =
            Trees(
                "paladin",
                "other-templar",
                "holy-spec");

        Assert.Equal(
            ["paladin", "other-templar", "holy-spec"],
            selector.BuildSectionKeys(
                globalTemplar,
                "holy"));

        Dictionary<string, TalentTreeViewModel> onlyTemplar =
            Trees(
                "paladin",
                "holy-templar",
                "holy-spec");

        Assert.Equal(
            ["paladin", "holy-templar", "holy-spec"],
            selector.BuildSectionKeys(
                onlyTemplar,
                "holy"));
    }

    [Fact]
    public void SelectorBuildsTreeKeysAndRemovesDuplicates()
    {
        var selector = new TalentPageTreeSelector();
        Dictionary<string, TalentTreeViewModel> trees =
            Trees("paladin", "holy");

        Assert.Equal(
            ["paladin", "holy"],
            selector.BuildTreeKeys(
                trees,
                "holy",
                "holy"));
    }

    [Fact]
    public void SelectorResolvesExactHeroSuffixTokensAndMissingKeys()
    {
        var selector = new TalentPageTreeSelector();

        Assert.Null(
            selector.ResolveTreeKey(
                " ",
                ["holy"],
                "holy"));

        Assert.Equal(
            "holy",
            selector.ResolveTreeKey(
                " HOLY ",
                ["holy"],
                "holy"));

        Assert.Equal(
            "holy-herald",
            selector.ResolveTreeKey(
                "herald",
                ["protection-herald", "holy-herald"],
                "holy"));

        Assert.Equal(
            "protection-herald",
            selector.ResolveTreeKey(
                "herald",
                ["protection-herald"],
                "holy"));

        Assert.Equal(
            "holy-custom-spec",
            selector.ResolveTreeKey(
                "spec",
                ["holy-custom-spec"],
                "holy"));

        Assert.Equal(
            "holy-templar",
            selector.ResolveTreeKey(
                "templar-holy",
                ["holy-templar"],
                "holy"));

        Assert.Null(
            selector.ResolveTreeKey(
                "unknown",
                ["holy", "paladin"],
                "holy"));
    }

    [Fact]
    public async Task ContentReaderLoadsSpellsAndItems()
    {
        await using AppDbContext db = CreateDb();

        db.Spells.Add(new Spell
        {
            Name = "Holy Shock"
        });
        db.Items.Add(new Item
        {
            Name = "Hammer"
        });

        await db.SaveChangesAsync();

        var reader =
            new TalentPageContentReader(db);

        TalentPageContent content =
            await reader.LoadAsync();

        Assert.Single(content.Spells);
        Assert.Single(content.Items);
        Assert.Equal(
            "Holy Shock",
            content.Spells[0].Name);
        Assert.Equal(
            "Hammer",
            content.Items[0].Name);

        Assert.Throws<ArgumentNullException>(
            () => new TalentPageContentReader(null!));
    }

    [Fact]
    public async Task ModelFactoryComposesContentTreesAndTitles()
    {
        var content =
            new Mock<ITalentPageContentReader>();
        var talentTrees =
            new Mock<ITalentTreeService>();

        var spell = new Spell
        {
            Name = "Holy Shock"
        };
        var item = new Item
        {
            Name = "Hammer"
        };
        Dictionary<string, TalentTreeViewModel> trees =
            Trees("paladin", "holy");

        content.Setup(x => x.LoadAsync())
            .ReturnsAsync(
                new TalentPageContent(
                    [spell],
                    [item]));

        talentTrees.Setup(x => x.GetTalentTrees(
                "holy",
                It.Is<List<Spell>>(spells =>
                    spells.Single() == spell)))
            .ReturnsAsync(trees);

        talentTrees.Setup(x => x.GetTalentTrees(
                "",
                It.IsAny<List<Spell>>()))
            .ReturnsAsync(
                new Dictionary<string, TalentTreeViewModel>());

        var factory =
            new TalentPageModelFactory(
                content.Object,
                talentTrees.Object);

        CombinedViewModel model =
            await factory.BuildAsync("holy");

        Assert.Equal("Holy", model.Section);
        Assert.Equal(
            "Holy Paladin – Talents",
            model.PageTitle);
        Assert.Same(spell, model.Spells.Single());
        Assert.Same(item, model.Items.Single());
        Assert.Same(trees, model.TalentTrees);

        CombinedViewModel empty =
            await factory.BuildAsync("");

        Assert.Equal("", empty.Section);
        Assert.Equal(
            " Paladin – Talents",
            empty.PageTitle);

        Assert.Throws<ArgumentNullException>(
            () => new TalentPageModelFactory(
                null!,
                talentTrees.Object));
        Assert.Throws<ArgumentNullException>(
            () => new TalentPageModelFactory(
                content.Object,
                null!));
    }

    [Fact]
    public async Task FacadeDelegatesNormalizationAndBuildsSection()
    {
        var models =
            new Mock<ITalentPageModelFactory>();
        var selector =
            new Mock<ITalentPageTreeSelector>();

        CombinedViewModel model = new()
        {
            TalentTrees = Trees(
                "paladin",
                "holy")
        };

        models.Setup(x => x.BuildAsync("holy"))
            .ReturnsAsync(model);
        selector.Setup(x =>
                x.NormalizeSection("holly"))
            .Returns("holy");
        selector.Setup(x =>
                x.ResolveTreeSection(
                    "holy-herald",
                    null))
            .Returns("holy");
        selector.Setup(x =>
                x.BuildSectionKeys(
                    model.TalentTrees,
                    "holy"))
            .Returns(["paladin", "holy"]);

        var service =
            new TalentPageService(
                models.Object,
                selector.Object);

        Assert.Equal(
            "holy",
            service.NormalizeSection("holly"));
        Assert.Equal(
            "holy",
            service.ResolveTreeSection(
                "holy-herald",
                null));

        TalentSectionData data =
            await service.BuildSectionAsync("holy");

        Assert.Equal("holy", data.Section);
        Assert.Equal(
            ["paladin", "holy"],
            data.Keys);
        Assert.Same(model, data.Model);

        Assert.Throws<ArgumentNullException>(
            () => new TalentPageService(
                null!,
                selector.Object));
        Assert.Throws<ArgumentNullException>(
            () => new TalentPageService(
                models.Object,
                null!));
    }

    [Fact]
    public async Task FacadeBuildTreeReturnsNullOrSelectedTrees()
    {
        var models =
            new Mock<ITalentPageModelFactory>();
        var selector =
            new Mock<ITalentPageTreeSelector>();

        CombinedViewModel model = new()
        {
            TalentTrees = Trees(
                "paladin",
                "holy",
                "holy-herald")
        };

        models.Setup(x => x.BuildAsync("holy"))
            .ReturnsAsync(model);

        selector.SetupSequence(x =>
                x.ResolveTreeKey(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    "holy"))
            .Returns((string?)null)
            .Returns("holy-herald");

        var service =
            new TalentPageService(
                models.Object,
                selector.Object);

        Assert.Null(
            await service.BuildTreeAsync(
                "holy",
                "missing"));

        selector.Setup(x =>
                x.BuildTreeKeys(
                    model.TalentTrees,
                    "holy",
                    "holy-herald"))
            .Returns(
                ["paladin", "holy-herald", "holy"]);

        TalentTreeData? result =
            await service.BuildTreeAsync(
                "holy",
                "herald");

        Assert.NotNull(result);
        Assert.Equal("holy", result!.Section);
        Assert.Equal("herald", result.RequestedKey);
        Assert.Equal(
            "holy-herald",
            result.ResolvedKey);
        Assert.Equal(
            3,
            result.SelectedTrees.Count);
        Assert.Same(
            model.TalentTrees["holy-herald"],
            result.SelectedTrees["holy-herald"]);
    }

    [Fact]
    public async Task LegacyConstructorPreservesExistingDbAndTreeServiceContract()
    {
        await using AppDbContext db = CreateDb();

        db.Spells.Add(new Spell
        {
            Name = "Templar Strike"
        });
        db.Items.Add(new Item
        {
            Name = "Relic"
        });
        await db.SaveChangesAsync();

        var talentTrees =
            new Mock<ITalentTreeService>();

        talentTrees.Setup(x => x.GetTalentTrees(
                "retribution",
                It.Is<List<Spell>>(spells =>
                    spells.Count == 1)))
            .ReturnsAsync(
                Trees(
                    "paladin",
                    "retribution",
                    "retribution-templar"));

        var service =
            new TalentPageService(
                db,
                talentTrees.Object);

        TalentSectionData data =
            await service.BuildSectionAsync(
                "retribution");

        Assert.Equal(
            ["paladin", "retribution-templar", "retribution"],
            data.Keys);
        Assert.Equal(
            "Retribution Paladin – Talents",
            data.Model.PageTitle);
    }

    private static Dictionary<string, TalentTreeViewModel>
        Trees(params string[] keys)
    {
        var result =
            new Dictionary<string, TalentTreeViewModel>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string key in keys)
        {
            result[key] = new TalentTreeViewModel
            {
                Key = key,
                Title = key
            };
        }

        return result;
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "talent-page-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
