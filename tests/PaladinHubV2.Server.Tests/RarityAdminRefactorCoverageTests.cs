using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class RarityAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsUsageAndHistory()
    {
        await using AppDbContext db =
            CreateDb();

        var rare = new ItemRarity
        {
            Name = "Rare",
            Description = "Blue",
            Color = "#0000ff",
            SortOrder = 2
        };

        var common = new ItemRarity
        {
            Name = "Common",
            Color = "#ffffff",
            SortOrder = 1
        };

        db.ItemRarities.AddRange(rare, common);
        await db.SaveChangesAsync(Ct);

        db.Items.AddRange(
            new Item
            {
                Name = "One",
                RarityId = rare.Id
            },
            new Item
            {
                Name = "Two",
                RarityId = rare.Id
            });

        db.RarityRevisions.AddRange(
            new RarityRevision
            {
                RarityId = rare.Id,
                Version = 1,
                Action = "created"
            },
            new RarityRevision
            {
                RarityId = rare.Id,
                Version = 4,
                Action = "updated"
            });

        await db.SaveChangesAsync(Ct);

        var service =
            new RarityAdminQueryService(db);

        List<RarityListItem> rows =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Common", "Rare"],
            rows.Select(row => row.Name));

        RarityListItem row =
            rows.Single(item =>
                item.Id == rare.Id);

        Assert.Equal(2, row.UsageCount);
        Assert.Equal(0, row.ChildCount);
        Assert.Null(row.ParentId);

        List<RarityRevision> history =
            await service.HistoryAsync(
                rare.Id,
                Ct);

        Assert.Equal(
            [4, 1],
            history.Select(item => item.Version));
    }

    [Fact]
    public async Task ValidatorCoversBlankDuplicateAndValidNames()
    {
        await using AppDbContext db =
            CreateDb();

        var active = new ItemRarity
        {
            Name = "Rare",
            Color = "#0000ff"
        };

        var deleted = new ItemRarity
        {
            Name = "Deleted",
            Color = "#ffffff",
            IsDeleted = true
        };

        db.ItemRarities.AddRange(
            active,
            deleted);

        await db.SaveChangesAsync(Ct);

        var validator =
            new RarityAdminValidator(db);

        Assert.Equal(
            "Name is required.",
            await validator.ValidateAsync(
                0,
                Request(" "),
                Ct));

        Assert.Equal(
            "A rarity with this name already exists.",
            await validator.ValidateAsync(
                0,
                Request(" rare "),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                active.Id,
                Request(" Rare "),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                0,
                Request("Deleted"),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                0,
                Request("Epic"),
                Ct));
    }

    [Fact]
    public async Task UsageGuardReturnsWhetherItemsReferenceRarity()
    {
        await using AppDbContext db =
            CreateDb();

        var rarity = new ItemRarity
        {
            Name = "Rare",
            Color = "#0000ff"
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        var guard =
            new RarityUsageGuard(db);

        Assert.False(
            await guard.IsInUseAsync(
                rarity.Id,
                Ct));

        db.Items.Add(
            new Item
            {
                Name = "Used item",
                RarityId = rarity.Id
            });

        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                rarity.Id,
                Ct));
    }

    [Fact]
    public async Task RevisionJournalRecordsAndReadsSnapshot()
    {
        await using AppDbContext db =
            CreateDb();

        var rarity = new ItemRarity
        {
            Name = "Epic",
            Description = "Purple",
            Color = "#a335ee",
            Version = 7
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        var journal =
            new RarityRevisionJournal(db);

        journal.Record(
            rarity,
            "updated",
            "admin");

        await db.SaveChangesAsync(Ct);

        RarityRevision revision =
            await db.RarityRevisions.SingleAsync(Ct);

        Assert.Equal(rarity.Id, revision.RarityId);
        Assert.Equal(7, revision.Version);
        Assert.Equal("updated", revision.Action);
        Assert.Equal("admin", revision.Actor);

        ItemRarity snapshot =
            journal.ReadSnapshot(revision);

        Assert.Equal("Epic", snapshot.Name);
        Assert.Equal("Purple", snapshot.Description);
        Assert.Equal("#a335ee", snapshot.Color);
        Assert.Equal(7, snapshot.Version);
    }

    [Fact]
    public async Task QualitySynchronizerUpdatesLegacyQualityProjection()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var rarity = new ItemRarity
        {
            Name = "Rare",
            Color = "#0070dd"
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        db.Items.AddRange(
            new Item
            {
                Name = "Affected",
                RarityId = rarity.Id,
                Quality = "Old"
            },
            new Item
            {
                Name = "Unaffected",
                Quality = "Old"
            });

        await db.SaveChangesAsync(Ct);

        rarity.Name = "Mythic";

        var synchronizer =
            new RarityQualitySynchronizer(db);

        await synchronizer.SyncAsync(
            rarity,
            Ct);

        List<Item> items =
            await db.Items
                .OrderBy(item => item.Name)
                .ToListAsync(Ct);

        Assert.Equal(
            "Mythic",
            items.Single(item =>
                item.Name == "Affected").Quality);

        Assert.Equal(
            "Old",
            items.Single(item =>
                item.Name == "Unaffected").Quality);
    }

    [Fact]
    public async Task FacadeForwardsQueriesAndCreatesWithValidation()
    {
        await using AppDbContext db =
            CreateDb();

        var queries =
            new Mock<IRarityAdminQueryService>();

        var validator =
            new Mock<IRarityAdminValidator>();

        var usage =
            new Mock<IRarityUsageGuard>();

        var journal =
            new Mock<IRarityRevisionJournal>();

        var quality =
            new Mock<IRarityQualitySynchronizer>();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new RarityListItem(
                    1,
                    "Rare",
                    "",
                    "#0070dd",
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
                [new RarityRevision
                {
                    RarityId = 1,
                    Version = 1
                }]);

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<int>(),
                It.IsAny<RarityRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad")
            .ReturnsAsync((string?)null);

        Assert.NotNull(
            new RarityAdminService(
                db,
                new GameDataAssignmentService(db)));

        RarityAdminService service =
            CreateService(
                db,
                queries.Object,
                validator.Object,
                usage.Object,
                journal.Object,
                quality.Object);

        Assert.Single(
            await service.ListAsync(Ct));

        Assert.Single(
            await service.HistoryAsync(1, Ct));

        RarityAdminResult invalid =
            await service.CreateAsync(
                Request("Bad"),
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.Validation,
            invalid.Error);
        Assert.Equal("bad", invalid.Message);

        RarityAdminResult created =
            await service.CreateAsync(
                new RarityRequest(
                    " Mythic ",
                    " Top tier ",
                    "#ff8800",
                    8,
                    false,
                    0),
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.None,
            created.Error);

        Assert.NotNull(created.Rarity);
        Assert.Equal(
            "Mythic",
            created.Rarity!.Name);
        Assert.Equal(
            "Top tier",
            created.Rarity.Description);
        Assert.Equal(
            "#ff8800",
            created.Rarity.Color);
        Assert.Equal(8, created.Rarity.SortOrder);

        journal.Verify(x => x.Record(
            created.Rarity,
            "created",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleValidationAndSuccess()
    {
        await using AppDbContext db =
            CreateDb();

        var validator =
            new Mock<IRarityAdminValidator>();

        var usage =
            new Mock<IRarityUsageGuard>();

        var journal =
            new Mock<IRarityRevisionJournal>();

        var quality =
            new Mock<IRarityQualitySynchronizer>();

        RarityAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                usage: usage.Object,
                journal: journal.Object,
                quality: quality.Object);

        Assert.Equal(
            RarityAdminError.NotFound,
            (await service.UpdateAsync(
                999,
                Request("Missing"),
                "admin",
                Ct)).Error);

        var rarity = new ItemRarity
        {
            Name = "Rare",
            Color = "#0070dd",
            Version = 2
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            RarityAdminError.Stale,
            (await service.UpdateAsync(
                rarity.Id,
                Request("Rare", version: 1),
                "admin",
                Ct)).Error);

        validator.SetupSequence(x => x.ValidateAsync(
                rarity.Id,
                It.IsAny<RarityRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid")
            .ReturnsAsync((string?)null);

        RarityAdminResult invalid =
            await service.UpdateAsync(
                rarity.Id,
                Request("Rare", version: 2),
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.Validation,
            invalid.Error);

        RarityAdminResult updated =
            await service.UpdateAsync(
                rarity.Id,
                new RarityRequest(
                    " Epic ",
                    " Purple ",
                    "#a335ee",
                    4,
                    true,
                    2),
                "editor",
                Ct);

        Assert.Equal(
            RarityAdminError.None,
            updated.Error);

        Assert.Equal("Epic", rarity.Name);
        Assert.Equal("Purple", rarity.Description);
        Assert.Equal("#a335ee", rarity.Color);
        Assert.Equal(4, rarity.SortOrder);
        Assert.True(rarity.IsArchived);
        Assert.Equal(3, rarity.Version);

        quality.Verify(x => x.SyncAsync(
            rarity,
            It.IsAny<CancellationToken>()),
            Times.Once);

        journal.Verify(x => x.Record(
            rarity,
            "archived",
            "editor"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db =
            CreateDb();

        var usage =
            new Mock<IRarityUsageGuard>();

        var journal =
            new Mock<IRarityRevisionJournal>();

        RarityAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            RarityAdminError.NotFound,
            (await service.DeleteAsync(
                999,
                1,
                "admin",
                Ct)).Error);

        var rarity = new ItemRarity
        {
            Name = "Rare",
            Color = "#0070dd",
            Version = 2
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            RarityAdminError.Stale,
            (await service.DeleteAsync(
                rarity.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.IsInUseAsync(
                rarity.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        RarityAdminResult inUse =
            await service.DeleteAsync(
                rarity.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.InUse,
            inUse.Error);
        Assert.NotNull(inUse.Message);

        RarityAdminResult deleted =
            await service.DeleteAsync(
                rarity.Id,
                2,
                "deleter",
                Ct);

        Assert.Equal(
            RarityAdminError.None,
            deleted.Error);

        Assert.True(rarity.IsDeleted);
        Assert.Equal(3, rarity.Version);

        journal.Verify(x => x.Record(
            rarity,
            "deleted",
            "deleter"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversAllFailureModesAndSuccess()
    {
        await using AppDbContext db =
            CreateDb();

        var validator =
            new Mock<IRarityAdminValidator>();

        var journal =
            new Mock<IRarityRevisionJournal>();

        var quality =
            new Mock<IRarityQualitySynchronizer>();

        RarityAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object,
                quality: quality.Object);

        Assert.Equal(
            RarityAdminError.NotFound,
            (await service.RestoreAsync(
                999,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        var rarity = new ItemRarity
        {
            Name = "Deleted",
            Color = "#ffffff",
            Version = 4,
            IsDeleted = true
        };

        db.ItemRarities.Add(rarity);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            RarityAdminError.Stale,
            (await service.RestoreAsync(
                rarity.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            RarityAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                rarity.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        var deletedRevision =
            new RarityRevision
            {
                RarityId = rarity.Id,
                Version = 2,
                Snapshot = "{}"
            };

        var goodRevision =
            new RarityRevision
            {
                RarityId = rarity.Id,
                Version = 1,
                Snapshot = "{}"
            };

        db.RarityRevisions.AddRange(
            deletedRevision,
            goodRevision);

        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(new ItemRarity
            {
                Name = "Deleted snapshot",
                Color = "#ffffff",
                IsDeleted = true
            });

        RarityAdminResult deletedSnapshot =
            await service.RestoreAsync(
                rarity.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.DeletedRevision,
            deletedSnapshot.Error);

        var restoredSnapshot =
            new ItemRarity
            {
                Name = "Epic",
                Description = "Historic",
                Color = "#a335ee",
                SortOrder = 6,
                IsArchived = true,
                IsDeleted = false
            };

        journal.Setup(x => x.ReadSnapshot(
                goodRevision))
            .Returns(restoredSnapshot);

        validator.SetupSequence(x => x.ValidateAsync(
                rarity.Id,
                It.IsAny<RarityRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conflict")
            .ReturnsAsync((string?)null);

        RarityAdminResult invalid =
            await service.RestoreAsync(
                rarity.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            RarityAdminError.Validation,
            invalid.Error);

        RarityAdminResult restored =
            await service.RestoreAsync(
                rarity.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "restorer",
                Ct);

        Assert.Equal(
            RarityAdminError.None,
            restored.Error);

        Assert.False(rarity.IsDeleted);
        Assert.True(rarity.IsArchived);
        Assert.Equal("Epic", rarity.Name);
        Assert.Equal("Historic", rarity.Description);
        Assert.Equal("#a335ee", rarity.Color);
        Assert.Equal(6, rarity.SortOrder);
        Assert.Equal(5, rarity.Version);

        quality.Verify(x => x.SyncAsync(
            rarity,
            It.IsAny<CancellationToken>()),
            Times.Once);

        journal.Verify(x => x.Record(
            rarity,
            "restored",
            "restorer"),
            Times.Once);
    }

    [Fact]
    public void MutationHelpersCoverActionsRestoreMappingAndApply()
    {
        Assert.Equal(
            "updated",
            RarityAdminService.ResolveUpdateAction(
                false,
                false));

        Assert.Equal(
            "archived",
            RarityAdminService.ResolveUpdateAction(
                false,
                true));

        Assert.Equal(
            "unarchived",
            RarityAdminService.ResolveUpdateAction(
                true,
                false));

        var snapshot = new ItemRarity
        {
            Name = "Epic",
            Description = "Purple",
            Color = "#a335ee",
            SortOrder = 4,
            IsArchived = true
        };

        RarityRequest request =
            RarityAdminService.BuildRestoreRequest(
                snapshot,
                7);

        Assert.Equal("Epic", request.Name);
        Assert.Equal("Purple", request.Description);
        Assert.Equal("#a335ee", request.Color);
        Assert.Equal(4, request.SortOrder);
        Assert.True(request.IsArchived);
        Assert.Equal(7, request.Version);

        var target = new ItemRarity();

        RarityAdminService.Apply(
            target,
            new RarityRequest(
                " Name ",
                null,
                "#123456",
                8,
                true,
                1));

        Assert.Equal("Name", target.Name);
        Assert.Equal(string.Empty, target.Description);
        Assert.Equal("#123456", target.Color);
        Assert.Equal(8, target.SortOrder);
        Assert.True(target.IsArchived);
    }

    private static RarityAdminService CreateService(
        AppDbContext db,
        IRarityAdminQueryService? queries = null,
        IRarityAdminValidator? validator = null,
        IRarityUsageGuard? usage = null,
        IRarityRevisionJournal? journal = null,
        IRarityQualitySynchronizer? quality = null)
    {
        return new RarityAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ?? new RarityAdminQueryService(db),
            validator ?? new RarityAdminValidator(db),
            usage ?? new RarityUsageGuard(db),
            journal ?? new RarityRevisionJournal(db),
            quality ?? Mock.Of<IRarityQualitySynchronizer>());
    }

    private static RarityRequest Request(
        string name,
        int version = 1)
    {
        return new RarityRequest(
            name,
            string.Empty,
            "#abcdef",
            0,
            false,
            version);
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "rarity-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
