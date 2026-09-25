using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class CategoryAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsUsageChildrenAndHistory()
    {
        await using AppDbContext db = CreateDb();

        var parent = new Category
        {
            Name = "Beta",
            Description = "Parent",
            SortOrder = 2
        };
        var alpha = new Category
        {
            Name = "Alpha",
            SortOrder = 1
        };
        db.Categories.AddRange(parent, alpha);
        await db.SaveChangesAsync(Ct);

        var child = new Category
        {
            Name = "Child",
            ParentId = parent.Id,
            SortOrder = 0
        };
        db.Categories.Add(child);
        db.Spells.Add(new Spell
        {
            Name = "Spell",
            CategoryId = parent.Id
        });
        db.Items.Add(new Item
        {
            Name = "Item",
            CategoryId = parent.Id
        });
        db.CategoryRevisions.AddRange(
            new CategoryRevision
            {
                CategoryId = parent.Id,
                Version = 1,
                Action = "created"
            },
            new CategoryRevision
            {
                CategoryId = parent.Id,
                Version = 4,
                Action = "updated"
            });
        await db.SaveChangesAsync(Ct);

        var service =
            new CategoryAdminQueryService(db);

        List<CategoryListItem> rows =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Child", "Alpha", "Beta"],
            rows.Select(row => row.Name));

        CategoryListItem row =
            rows.Single(item => item.Id == parent.Id);

        Assert.Equal(2, row.UsageCount);
        Assert.Equal(1, row.ChildCount);

        List<CategoryRevision> history =
            await service.HistoryAsync(
                parent.Id,
                Ct);

        Assert.Equal(
            [4, 1],
            history.Select(item => item.Version));
    }

    [Fact]
    public async Task ValidatorCoversHierarchyAndArchiveRules()
    {
        await using AppDbContext db = CreateDb();

        var root = new Category
        {
            Name = "Root"
        };
        var archived = new Category
        {
            Name = "Archived",
            IsArchived = true
        };
        db.Categories.AddRange(root, archived);
        await db.SaveChangesAsync(Ct);

        var child = new Category
        {
            Name = "Child",
            ParentId = root.Id
        };
        var deleted = new Category
        {
            Name = "Deleted",
            IsDeleted = true
        };
        db.Categories.AddRange(child, deleted);
        await db.SaveChangesAsync(Ct);

        var validator =
            new CategoryAdminValidator(db);

        Assert.Equal(
            "Name is required.",
            await validator.ValidateAsync(
                0,
                Request(" "),
                Ct));

        Assert.Equal(
            "A category with this name already exists under this parent.",
            await validator.ValidateAsync(
                0,
                Request(" root "),
                Ct));

        Assert.Equal(
            "A category cannot be placed inside itself or its descendants.",
            await validator.ValidateAsync(
                root.Id,
                Request(
                    "Root",
                    root.Id,
                    version: root.Version),
                Ct));

        Assert.Equal(
            "A category cannot be placed inside itself or its descendants.",
            await validator.ValidateAsync(
                root.Id,
                Request(
                    "Root",
                    child.Id,
                    version: root.Version),
                Ct));

        Assert.Equal(
            "Parent category does not exist. Restore it first.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Missing",
                    99999),
                Ct));

        Assert.Equal(
            "Parent category does not exist. Restore it first.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Deleted child",
                    deleted.Id),
                Ct));

        Assert.Equal(
            "An active category cannot have an archived parent.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Active child",
                    archived.Id),
                Ct));

        Assert.Equal(
            "Archive or move the active subcategories first.",
            await validator.ValidateAsync(
                root.Id,
                Request(
                    "Root",
                    archived: true,
                    version: root.Version),
                Ct));

        child.IsArchived = true;
        await db.SaveChangesAsync(Ct);

        Assert.Null(
            await validator.ValidateAsync(
                root.Id,
                Request(
                    " Root Prime ",
                    archived: true,
                    version: root.Version),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                0,
                Request(
                    "Archived child",
                    archived.Id,
                    archived: true),
                Ct));
    }

    [Fact]
    public async Task UsageGuardChecksChildrenSpellsItemsAndUnused()
    {
        await using AppDbContext db = CreateDb();

        var parent = new Category
        {
            Name = "Parent"
        };
        db.Categories.Add(parent);
        await db.SaveChangesAsync(Ct);

        var child = new Category
        {
            Name = "Child",
            ParentId = parent.Id
        };
        db.Categories.Add(child);
        await db.SaveChangesAsync(Ct);

        var guard = new CategoryUsageGuard(db);

        Assert.True(
            await guard.IsInUseAsync(
                parent.Id,
                Ct));

        child.IsDeleted = true;
        db.Spells.Add(new Spell
        {
            Name = "Spell",
            CategoryId = parent.Id
        });
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                parent.Id,
                Ct));

        db.Spells.RemoveRange(db.Spells);
        db.Items.Add(new Item
        {
            Name = "Item",
            CategoryId = parent.Id
        });
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                parent.Id,
                Ct));

        db.Items.RemoveRange(db.Items);
        await db.SaveChangesAsync(Ct);

        Assert.False(
            await guard.IsInUseAsync(
                parent.Id,
                Ct));
    }

    [Fact]
    public async Task RevisionJournalRecordsAndReadsSnapshots()
    {
        await using AppDbContext db = CreateDb();

        var category = new Category
        {
            Name = "Consumables",
            Description = "Items",
            Version = 5
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);

        var journal =
            new CategoryRevisionJournal(db);

        journal.Record(
            category,
            "updated",
            "admin");
        await db.SaveChangesAsync(Ct);

        CategoryRevision revision =
            await db.CategoryRevisions.SingleAsync(Ct);

        Assert.Equal(category.Id, revision.CategoryId);
        Assert.Equal(5, revision.Version);
        Assert.Equal("updated", revision.Action);
        Assert.Equal("admin", revision.Actor);

        Category snapshot =
            journal.ReadSnapshot(revision);

        Assert.Equal("Consumables", snapshot.Name);
        Assert.Equal("Items", snapshot.Description);
        Assert.Equal(5, snapshot.Version);
    }

    [Fact]
    public async Task FacadeForwardsQueriesAndCreatesWithValidation()
    {
        await using AppDbContext db = CreateDb();

        var queries =
            new Mock<ICategoryAdminQueryService>();
        var validator =
            new Mock<ICategoryAdminValidator>();
        var usage =
            new Mock<ICategoryUsageGuard>();
        var journal =
            new Mock<ICategoryRevisionJournal>();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CategoryListItem(
                    1,
                    "Consumables",
                    "",
                    null,
                    0,
                    false,
                    false,
                    1,
                    0,
                    0)]);

        queries.Setup(x => x.HistoryAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new CategoryRevision
                {
                    CategoryId = 1,
                    Version = 1
                }]);

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<int>(),
                It.IsAny<CategoryRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad")
            .ReturnsAsync((string?)null);

        Assert.NotNull(
            new CategoryAdminService(
                db,
                new GameDataAssignmentService(db)));

        CategoryAdminService service =
            CreateService(
                db,
                queries.Object,
                validator.Object,
                usage.Object,
                journal.Object);

        Assert.Single(await service.ListAsync(Ct));
        Assert.Single(await service.HistoryAsync(1, Ct));

        CategoryAdminResult invalid =
            await service.CreateAsync(
                Request("Bad"),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.Validation,
            invalid.Error);
        Assert.Equal("bad", invalid.Message);

        CategoryAdminResult created =
            await service.CreateAsync(
                Request(
                    " Consumables ",
                    description: " items "),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.None,
            created.Error);
        Assert.NotNull(created.Category);
        Assert.Equal(
            "Consumables",
            created.Category!.Name);
        Assert.Equal(
            "items",
            created.Category.Description);

        journal.Verify(x => x.Record(
            created.Category,
            "created",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleValidationAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<ICategoryAdminValidator>();
        var usage =
            new Mock<ICategoryUsageGuard>();
        var journal =
            new Mock<ICategoryRevisionJournal>();

        CategoryAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            CategoryAdminError.NotFound,
            (await service.UpdateAsync(
                99,
                Request("Missing"),
                "admin",
                Ct)).Error);

        var category = new Category
        {
            Name = "Old",
            Version = 2
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            CategoryAdminError.Stale,
            (await service.UpdateAsync(
                category.Id,
                Request(
                    "Old",
                    version: 1),
                "admin",
                Ct)).Error);

        validator.SetupSequence(x => x.ValidateAsync(
                category.Id,
                It.IsAny<CategoryRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid")
            .ReturnsAsync((string?)null);

        CategoryAdminResult invalid =
            await service.UpdateAsync(
                category.Id,
                Request(
                    "Old",
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.Validation,
            invalid.Error);

        CategoryAdminResult updated =
            await service.UpdateAsync(
                category.Id,
                Request(
                    " New ",
                    description: " changed ",
                    sortOrder: 7,
                    archived: true,
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.None,
            updated.Error);
        Assert.Equal(3, category.Version);
        Assert.True(category.IsArchived);
        Assert.Equal("New", category.Name);
        Assert.Equal("changed", category.Description);
        Assert.Equal(7, category.SortOrder);

        journal.Verify(x => x.Record(
            category,
            "archived",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var usage =
            new Mock<ICategoryUsageGuard>();
        var journal =
            new Mock<ICategoryRevisionJournal>();

        CategoryAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            CategoryAdminError.NotFound,
            (await service.DeleteAsync(
                99,
                1,
                "admin",
                Ct)).Error);

        var category = new Category
        {
            Name = "Category",
            Version = 2
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            CategoryAdminError.Stale,
            (await service.DeleteAsync(
                category.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.IsInUseAsync(
                category.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        CategoryAdminResult inUse =
            await service.DeleteAsync(
                category.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.InUse,
            inUse.Error);
        Assert.NotNull(inUse.Message);

        CategoryAdminResult deleted =
            await service.DeleteAsync(
                category.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.None,
            deleted.Error);
        Assert.True(category.IsDeleted);
        Assert.Equal(3, category.Version);

        journal.Verify(x => x.Record(
            category,
            "deleted",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversAllFailureModesAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<ICategoryAdminValidator>();
        var journal =
            new Mock<ICategoryRevisionJournal>();

        CategoryAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            CategoryAdminError.NotFound,
            (await service.RestoreAsync(
                99,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        var category = new Category
        {
            Name = "Current",
            Version = 4,
            IsDeleted = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            CategoryAdminError.Stale,
            (await service.RestoreAsync(
                category.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            CategoryAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                category.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        var deletedRevision =
            new CategoryRevision
            {
                CategoryId = category.Id,
                Version = 2,
                Snapshot = "{}"
            };
        var goodRevision =
            new CategoryRevision
            {
                CategoryId = category.Id,
                Version = 1,
                Snapshot = "{}"
            };

        db.CategoryRevisions.AddRange(
            deletedRevision,
            goodRevision);
        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(new Category
            {
                Name = "Deleted snapshot",
                IsDeleted = true
            });

        CategoryAdminResult deletedSnapshot =
            await service.RestoreAsync(
                category.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.DeletedRevision,
            deletedSnapshot.Error);

        var restoredSnapshot = new Category
        {
            Name = "Recovered",
            Description = "Historical",
            ParentId = null,
            SortOrder = 9,
            IsArchived = true,
            IsDeleted = false
        };

        journal.Setup(x => x.ReadSnapshot(
                goodRevision))
            .Returns(restoredSnapshot);

        validator.SetupSequence(x => x.ValidateAsync(
                category.Id,
                It.IsAny<CategoryRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conflict")
            .ReturnsAsync((string?)null);

        CategoryAdminResult invalid =
            await service.RestoreAsync(
                category.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.Validation,
            invalid.Error);

        CategoryAdminResult restored =
            await service.RestoreAsync(
                category.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            CategoryAdminError.None,
            restored.Error);
        Assert.False(category.IsDeleted);
        Assert.True(category.IsArchived);
        Assert.Equal("Recovered", category.Name);
        Assert.Equal("Historical", category.Description);
        Assert.Equal(9, category.SortOrder);
        Assert.Equal(5, category.Version);

        journal.Verify(x => x.Record(
            category,
            "restored",
            "admin"),
            Times.Once);
    }

    [Fact]
    public void MutationHelpersCoverActionsRestoreMappingAndApply()
    {
        Assert.Equal(
            "updated",
            CategoryAdminService.ResolveUpdateAction(
                false,
                false));
        Assert.Equal(
            "archived",
            CategoryAdminService.ResolveUpdateAction(
                false,
                true));
        Assert.Equal(
            "unarchived",
            CategoryAdminService.ResolveUpdateAction(
                true,
                false));

        var snapshot = new Category
        {
            Name = "Category",
            Description = "Description",
            ParentId = 3,
            SortOrder = 9,
            IsArchived = true
        };

        CategoryRequest request =
            CategoryAdminService.BuildRestoreRequest(
                snapshot,
                7);

        Assert.Equal("Category", request.Name);
        Assert.Equal("Description", request.Description);
        Assert.Equal(3, request.ParentId);
        Assert.Equal(9, request.SortOrder);
        Assert.True(request.IsArchived);
        Assert.Equal(7, request.Version);

        var target = new Category();

        CategoryAdminService.Apply(
            target,
            new CategoryRequest(
                " Name ",
                null,
                2,
                4,
                true,
                1));

        Assert.Equal("Name", target.Name);
        Assert.Equal(string.Empty, target.Description);
        Assert.Equal(2, target.ParentId);
        Assert.Equal(4, target.SortOrder);
        Assert.True(target.IsArchived);
    }

    private static CategoryAdminService CreateService(
        AppDbContext db,
        ICategoryAdminQueryService? queries = null,
        ICategoryAdminValidator? validator = null,
        ICategoryUsageGuard? usage = null,
        ICategoryRevisionJournal? journal = null)
    {
        return new CategoryAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ?? new CategoryAdminQueryService(db),
            validator ?? new CategoryAdminValidator(db),
            usage ?? new CategoryUsageGuard(db),
            journal ?? new CategoryRevisionJournal(db));
    }

    private static CategoryRequest Request(
        string name,
        int? parentId = null,
        string? description = null,
        int sortOrder = 0,
        bool archived = false,
        int version = 1) =>
        new(
            name,
            description,
            parentId,
            sortOrder,
            archived,
            version);

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "category-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
