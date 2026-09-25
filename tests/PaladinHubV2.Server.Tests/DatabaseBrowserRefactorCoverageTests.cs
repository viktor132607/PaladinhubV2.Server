using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class DatabaseBrowserRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScopeResolverCoversCategoryHierarchyAndDisciplineStates()
    {
        await using AppDbContext db = CreateDb();

        db.Categories.AddRange(
            new Category
            {
                Id = 1,
                Name = "Root"
            },
            new Category
            {
                Id = 2,
                Name = "Child",
                ParentId = 1
            },
            new Category
            {
                Id = 3,
                Name = "Grandchild",
                ParentId = 2
            },
            new Category
            {
                Id = 4,
                Name = "Deleted",
                ParentId = 1,
                IsDeleted = true
            });

        db.GameDisciplines.AddRange(
            new GameDiscipline
            {
                Id = 10,
                Name = "Paladin"
            },
            new GameDiscipline
            {
                Id = 11,
                Name = "Holy",
                ParentId = 10
            },
            new GameDiscipline
            {
                Id = 12,
                Name = "Deleted spec",
                ParentId = 10,
                IsDeleted = true
            });

        await db.SaveChangesAsync(Ct);

        var resolver =
            new DatabaseBrowseScopeResolver(db);

        Assert.Empty(
            await resolver.ResolveCategoryIdsAsync(
                null,
                Ct));

        Assert.Empty(
            await resolver.ResolveCategoryIdsAsync(
                999,
                Ct));

        HashSet<int> categories =
            await resolver.ResolveCategoryIdsAsync(
                1,
                Ct);

        Assert.Equal(
            new[] { 1, 2, 3 },
            categories.OrderBy(x => x));

        Assert.Empty(
            await resolver.ResolveDisciplineIdsAsync(
                null,
                Ct) ??
            []);

        Assert.Null(
            await resolver.ResolveDisciplineIdsAsync(
                999,
                Ct));

        List<int>? disciplines =
            await resolver.ResolveDisciplineIdsAsync(
                10,
                Ct);

        Assert.NotNull(disciplines);
        Assert.Equal(
            new[] { 10, 11 },
            disciplines!.OrderBy(x => x));
    }

    [Fact]
    public async Task SpellQueryCoversZeroAndPositiveFiltersSearchTagsPagingAndSorting()
    {
        await using AppDbContext db = CreateDb();

        db.GameTags.AddRange(
            new GameTag
            {
                Id = 5,
                Name = "Healing"
            },
            new GameTag
            {
                Id = 6,
                Name = "DeletedTag",
                IsDeleted = true
            });

        db.Spells.AddRange(
            new Spell
            {
                Id = 1,
                Name = "Alpha",
                Description = "restoration",
                CategoryId = 2,
                DisciplineId = 11,
                PatchId = 7,
                TagIds = [5]
            },
            new Spell
            {
                Id = 2,
                Name = "Beta",
                Description = "plain",
                CategoryId = null,
                DisciplineId = null,
                PatchId = null,
                TagIds = []
            },
            new Spell
            {
                Id = 3,
                Name = "Gamma",
                Description = null,
                CategoryId = 2,
                DisciplineId = 11,
                PatchId = 7,
                TagIds = [6]
            });

        await db.SaveChangesAsync(Ct);

        var query =
            new DatabaseSpellBrowseQuery(db);

        var zeroModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Spells,
                Page = 5,
                PageSize = 10
            };

        await query.PopulateAsync(
            zeroModel,
            new DatabaseBrowseFilters(
                "",
                0,
                [],
                0,
                [],
                0,
                0,
                null),
            Ct);

        Assert.Equal(1, zeroModel.Total);
        Assert.Equal(1, zeroModel.Page);
        Assert.Equal(
            "Beta",
            Assert.Single(zeroModel.Spells!).Name);

        var positiveModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Spells,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            positiveModel,
            new DatabaseBrowseFilters(
                "Healing",
                1,
                [1, 2],
                10,
                [10, 11],
                5,
                7,
                null),
            Ct);

        Assert.Equal(1, positiveModel.Total);
        Assert.Equal(
            "Alpha",
            Assert.Single(
                positiveModel.Spells!).Name);

        var descriptionModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Spells,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            descriptionModel,
            new DatabaseBrowseFilters(
                "restoration",
                null,
                [],
                null,
                [],
                null,
                null,
                null),
            Ct);

        Assert.Equal(
            "Alpha",
            Assert.Single(
                descriptionModel.Spells!).Name);

        var nameModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Spells,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            nameModel,
            new DatabaseBrowseFilters(
                "Gamma",
                null,
                [],
                null,
                [],
                null,
                null,
                null),
            Ct);

        Assert.Equal(
            "Gamma",
            Assert.Single(
                nameModel.Spells!).Name);
    }

    [Fact]
    public async Task ItemQueryCoversZeroAndPositiveFiltersRaritySearchTagsPagingAndSorting()
    {
        await using AppDbContext db = CreateDb();

        db.GameTags.Add(
            new GameTag
            {
                Id = 5,
                Name = "Weapon"
            });

        db.Items.AddRange(
            new Item
            {
                Id = 1,
                Name = "Alpha Item",
                Description = "legendary blade",
                CategoryId = 2,
                DisciplineId = 11,
                PatchId = 7,
                RarityId = 9,
                TagIds = [5]
            },
            new Item
            {
                Id = 2,
                Name = "Beta Item",
                Description = "plain",
                CategoryId = null,
                DisciplineId = null,
                PatchId = null,
                RarityId = null,
                TagIds = []
            });

        await db.SaveChangesAsync(Ct);

        var query =
            new DatabaseItemBrowseQuery(db);

        var zeroModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Items,
                Page = 4,
                PageSize = 10
            };

        await query.PopulateAsync(
            zeroModel,
            new DatabaseBrowseFilters(
                "",
                0,
                [],
                0,
                [],
                0,
                0,
                0),
            Ct);

        Assert.Equal(1, zeroModel.Total);
        Assert.Equal(1, zeroModel.Page);
        Assert.Equal(
            "Beta Item",
            Assert.Single(
                zeroModel.Items!).Name);

        var positiveModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Items,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            positiveModel,
            new DatabaseBrowseFilters(
                "Weapon",
                1,
                [1, 2],
                10,
                [10, 11],
                5,
                7,
                9),
            Ct);

        Assert.Equal(1, positiveModel.Total);
        Assert.Equal(
            "Alpha Item",
            Assert.Single(
                positiveModel.Items!).Name);

        var descriptionModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Items,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            descriptionModel,
            new DatabaseBrowseFilters(
                "legendary",
                null,
                [],
                null,
                [],
                null,
                null,
                null),
            Ct);

        Assert.Equal(
            "Alpha Item",
            Assert.Single(
                descriptionModel.Items!).Name);

        var nameModel =
            new AdminDatabaseIndexViewModel
            {
                Entity = AdminEntity.Items,
                Page = 1,
                PageSize = 10
            };

        await query.PopulateAsync(
            nameModel,
            new DatabaseBrowseFilters(
                "Beta Item",
                null,
                [],
                null,
                [],
                null,
                null,
                null),
            Ct);

        Assert.Equal(
            "Beta Item",
            Assert.Single(
                nameModel.Items!).Name);
    }

    [Fact]
    public async Task BrowserOrchestrationCoversValidationNormalizationAndBothEntities()
    {
        await using AppDbContext db = CreateDb();

        Assert.NotNull(
            new DatabaseBrowserService(db));

        var scopes =
            new Mock<IDatabaseBrowseScopeResolver>();

        var spells =
            new Mock<IDatabaseSpellBrowseQuery>();

        var items =
            new Mock<IDatabaseItemBrowseQuery>();

        scopes.Setup(x =>
                x.ResolveCategoryIdsAsync(
                    99,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service =
            new DatabaseBrowserService(
                scopes.Object,
                spells.Object,
                items.Object);

        DatabaseBrowseResult missingCategory =
            await service.BrowseAsync(
                "Spells",
                null,
                1,
                20,
                99,
                null,
                null,
                null,
                null,
                Ct);

        Assert.Equal(
            DatabaseBrowseError.CategoryNotFound,
            missingCategory.Error);

        scopes.Setup(x =>
                x.ResolveCategoryIdsAsync(
                    It.Is<int?>(value =>
                        value != 99),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        scopes.Setup(x =>
                x.ResolveDisciplineIdsAsync(
                    88,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (List<int>?)null);

        DatabaseBrowseResult missingDiscipline =
            await service.BrowseAsync(
                "Spells",
                null,
                1,
                20,
                null,
                88,
                null,
                null,
                null,
                Ct);

        Assert.Equal(
            DatabaseBrowseError.DisciplineNotFound,
            missingDiscipline.Error);

        scopes.Setup(x =>
                x.ResolveDisciplineIdsAsync(
                    It.Is<int?>(value =>
                        value != 88),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        spells.Setup(x => x.PopulateAsync(
                It.IsAny<AdminDatabaseIndexViewModel>(),
                It.IsAny<DatabaseBrowseFilters>(),
                It.IsAny<CancellationToken>()))
            .Callback<
                AdminDatabaseIndexViewModel,
                DatabaseBrowseFilters,
                CancellationToken>(
                (model, _, _) =>
                {
                    model.Total = 0;
                    DatabaseBrowserService.ClampPage(
                        model);
                    model.Spells = [];
                })
            .Returns(Task.CompletedTask);

        items.Setup(x => x.PopulateAsync(
                It.IsAny<AdminDatabaseIndexViewModel>(),
                It.IsAny<DatabaseBrowseFilters>(),
                It.IsAny<CancellationToken>()))
            .Callback<
                AdminDatabaseIndexViewModel,
                DatabaseBrowseFilters,
                CancellationToken>(
                (model, _, _) =>
                {
                    model.Total = 0;
                    DatabaseBrowserService.ClampPage(
                        model);
                    model.Items = [];
                })
            .Returns(Task.CompletedTask);

        DatabaseBrowseResult spellResult =
            await service.BrowseAsync(
                "unknown",
                "  heal  ",
                0,
                500,
                null,
                null,
                5,
                7,
                9,
                Ct);

        Assert.Equal(
            DatabaseBrowseError.None,
            spellResult.Error);
        Assert.NotNull(spellResult.Model);
        Assert.Equal(
            AdminEntity.Spells,
            spellResult.Model!.Entity);
        Assert.Equal(
            "heal",
            spellResult.Model.Search);
        Assert.Equal(1, spellResult.Model.Page);
        Assert.Equal(
            100,
            spellResult.Model.PageSize);

        DatabaseBrowseResult itemResult =
            await service.BrowseAsync(
                " items ",
                " ",
                2,
                0,
                null,
                null,
                null,
                null,
                null,
                Ct);

        Assert.Equal(
            AdminEntity.Items,
            itemResult.Model!.Entity);
        Assert.Equal(
            string.Empty,
            itemResult.Model.Search);
        Assert.Equal(1, itemResult.Model.PageSize);

        spells.Verify(
            x => x.PopulateAsync(
                It.IsAny<AdminDatabaseIndexViewModel>(),
                It.IsAny<DatabaseBrowseFilters>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        items.Verify(
            x => x.PopulateAsync(
                It.IsAny<AdminDatabaseIndexViewModel>(),
                It.IsAny<DatabaseBrowseFilters>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void HelpersCoverEntityParsingAndPageClamp()
    {
        Assert.Equal(
            AdminEntity.Items,
            DatabaseBrowserService.ParseEntity(
                " ITEMS "));

        Assert.Equal(
            AdminEntity.Spells,
            DatabaseBrowserService.ParseEntity(
                null));

        var model =
            new AdminDatabaseIndexViewModel
            {
                Total = 21,
                Page = 99,
                PageSize = 10
            };

        DatabaseBrowserService.ClampPage(model);

        Assert.Equal(3, model.Page);

        model.Total = 0;
        model.Page = 2;

        DatabaseBrowserService.ClampPage(model);

        Assert.Equal(1, model.Page);
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "database-browser-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
