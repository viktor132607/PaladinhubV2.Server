using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class PatchAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsUsageAndHistory()
    {
        await using AppDbContext db = CreateDb();

        var beta = new GamePatch
        {
            Name = "Beta",
            Description = "Beta patch",
            SortOrder = 2
        };
        var alpha = new GamePatch
        {
            Name = "Alpha",
            SortOrder = 1
        };

        db.GamePatches.AddRange(beta, alpha);
        await db.SaveChangesAsync(Ct);

        db.Spells.Add(new Spell
        {
            Name = "Spell",
            PatchId = beta.Id
        });
        db.Items.Add(new Item
        {
            Name = "Item",
            PatchId = beta.Id
        });
        db.PatchRevisions.AddRange(
            new PatchRevision
            {
                PatchId = beta.Id,
                Version = 1,
                Action = "created"
            },
            new PatchRevision
            {
                PatchId = beta.Id,
                Version = 4,
                Action = "updated"
            });

        await db.SaveChangesAsync(Ct);

        var service =
            new PatchAdminQueryService(db);

        List<PatchListItem> rows =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Alpha", "Beta"],
            rows.Select(row => row.Name));

        PatchListItem row =
            rows.Single(item => item.Id == beta.Id);

        Assert.Equal(2, row.UsageCount);

        List<PatchRevision> history =
            await service.HistoryAsync(
                beta.Id,
                Ct);

        Assert.Equal(
            [4, 1],
            history.Select(item => item.Version));
    }

    [Fact]
    public async Task ValidatorCoversRequiredDuplicateDeletedAndValidNames()
    {
        await using AppDbContext db = CreateDb();

        var active = new GamePatch
        {
            Name = "Dragonflight"
        };
        var deleted = new GamePatch
        {
            Name = "Deleted",
            IsDeleted = true
        };

        db.GamePatches.AddRange(
            active,
            deleted);

        await db.SaveChangesAsync(Ct);

        var validator =
            new PatchAdminValidator(db);

        Assert.Equal(
            "Name is required.",
            await validator.ValidateAsync(
                0,
                Request(" "),
                Ct));

        Assert.Equal(
            "A patch with this name already exists.",
            await validator.ValidateAsync(
                0,
                Request(" dragonflight "),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                active.Id,
                Request(" Dragonflight "),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                0,
                Request(" Deleted "),
                Ct));

        Assert.Null(
            await validator.ValidateAsync(
                0,
                Request(" The War Within "),
                Ct));
    }

    [Fact]
    public async Task UsageGuardChecksSpellsItemsAndUnused()
    {
        await using AppDbContext db = CreateDb();

        var patch = new GamePatch
        {
            Name = "Patch"
        };

        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(Ct);

        var guard =
            new PatchUsageGuard(db);

        Assert.False(
            await guard.IsInUseAsync(
                patch.Id,
                Ct));

        var spell = new Spell
        {
            Name = "Spell",
            PatchId = patch.Id
        };
        db.Spells.Add(spell);
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                patch.Id,
                Ct));

        db.Spells.Remove(spell);

        var item = new Item
        {
            Name = "Item",
            PatchId = patch.Id
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                patch.Id,
                Ct));

        db.Items.Remove(item);
        await db.SaveChangesAsync(Ct);

        Assert.False(
            await guard.IsInUseAsync(
                patch.Id,
                Ct));
    }

    [Fact]
    public async Task RevisionJournalRecordsAndReadsSnapshots()
    {
        await using AppDbContext db = CreateDb();

        var patch = new GamePatch
        {
            Name = "Patch",
            Description = "Description",
            Version = 5
        };

        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(Ct);

        var journal =
            new PatchRevisionJournal(db);

        journal.Record(
            patch,
            "updated",
            "admin");

        await db.SaveChangesAsync(Ct);

        PatchRevision revision =
            await db.PatchRevisions.SingleAsync(Ct);

        Assert.Equal(
            patch.Id,
            revision.PatchId);

        Assert.Equal(
            5,
            revision.Version);

        Assert.Equal(
            "updated",
            revision.Action);

        Assert.Equal(
            "admin",
            revision.Actor);

        GamePatch snapshot =
            journal.ReadSnapshot(revision);

        Assert.Equal(
            "Patch",
            snapshot.Name);

        Assert.Equal(
            "Description",
            snapshot.Description);

        Assert.Equal(
            5,
            snapshot.Version);
    }

    [Fact]
    public async Task FacadeForwardsQueriesAndCreatesWithValidation()
    {
        await using AppDbContext db = CreateDb();

        var queries =
            new Mock<IPatchAdminQueryService>();

        var validator =
            new Mock<IPatchAdminValidator>();

        var usage =
            new Mock<IPatchUsageGuard>();

        var journal =
            new Mock<IPatchRevisionJournal>();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new PatchListItem(
                    1,
                    "Patch",
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
                [new PatchRevision
                {
                    PatchId = 1,
                    Version = 1
                }]);

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<int>(),
                It.IsAny<PatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad")
            .ReturnsAsync((string?)null);

        Assert.NotNull(
            new PatchAdminService(
                db,
                new GameDataAssignmentService(db)));

        PatchAdminService service =
            CreateService(
                db,
                queries.Object,
                validator.Object,
                usage.Object,
                journal.Object);

        Assert.Single(
            await service.ListAsync(Ct));

        Assert.Single(
            await service.HistoryAsync(
                1,
                Ct));

        PatchAdminResult invalid =
            await service.CreateAsync(
                Request("Bad"),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.Validation,
            invalid.Error);

        Assert.Equal(
            "bad",
            invalid.Message);

        PatchAdminResult created =
            await service.CreateAsync(
                Request(
                    " Patch 11.0 ",
                    " launch ",
                    7),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.None,
            created.Error);

        Assert.NotNull(
            created.Patch);

        Assert.Equal(
            "Patch 11.0",
            created.Patch!.Name);

        Assert.Equal(
            "launch",
            created.Patch.Description);

        journal.Verify(x => x.Record(
                created.Patch,
                "created",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleValidationAndAllActions()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<IPatchAdminValidator>();

        var journal =
            new Mock<IPatchRevisionJournal>();

        PatchAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            PatchAdminError.NotFound,
            (await service.UpdateAsync(
                999,
                Request("Missing"),
                "admin",
                Ct)).Error);

        var patch = new GamePatch
        {
            Name = "Old",
            Version = 2
        };

        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            PatchAdminError.Stale,
            (await service.UpdateAsync(
                patch.Id,
                Request(
                    "Old",
                    version: 1),
                "admin",
                Ct)).Error);

        validator.SetupSequence(x => x.ValidateAsync(
                patch.Id,
                It.IsAny<PatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid")
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null);

        PatchAdminResult invalid =
            await service.UpdateAsync(
                patch.Id,
                Request(
                    "Old",
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.Validation,
            invalid.Error);

        PatchAdminResult archived =
            await service.UpdateAsync(
                patch.Id,
                Request(
                    " New ",
                    " changed ",
                    7,
                    true,
                    2),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.None,
            archived.Error);

        Assert.Equal(
            3,
            patch.Version);

        Assert.True(
            patch.IsArchived);

        Assert.Equal(
            "New",
            patch.Name);

        Assert.Equal(
            "changed",
            patch.Description);

        Assert.Equal(
            7,
            patch.SortOrder);

        journal.Verify(x => x.Record(
                patch,
                "archived",
                "admin"),
            Times.Once);

        PatchAdminResult unarchived =
            await service.UpdateAsync(
                patch.Id,
                Request(
                    "New",
                    "changed",
                    7,
                    false,
                    3),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.None,
            unarchived.Error);

        Assert.False(
            patch.IsArchived);

        journal.Verify(x => x.Record(
                patch,
                "unarchived",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var usage =
            new Mock<IPatchUsageGuard>();

        var journal =
            new Mock<IPatchRevisionJournal>();

        PatchAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            PatchAdminError.NotFound,
            (await service.DeleteAsync(
                999,
                1,
                "admin",
                Ct)).Error);

        var patch = new GamePatch
        {
            Name = "Patch",
            Version = 2
        };

        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            PatchAdminError.Stale,
            (await service.DeleteAsync(
                patch.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.IsInUseAsync(
                patch.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        PatchAdminResult inUse =
            await service.DeleteAsync(
                patch.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.InUse,
            inUse.Error);

        Assert.Contains(
            "Remove this patch",
            inUse.Message);

        PatchAdminResult deleted =
            await service.DeleteAsync(
                patch.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.None,
            deleted.Error);

        Assert.True(
            patch.IsDeleted);

        Assert.Equal(
            3,
            patch.Version);

        journal.Verify(x => x.Record(
                patch,
                "deleted",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversAllFailureModesAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<IPatchAdminValidator>();

        var journal =
            new Mock<IPatchRevisionJournal>();

        PatchAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            PatchAdminError.NotFound,
            (await service.RestoreAsync(
                999,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        var patch = new GamePatch
        {
            Name = "Current",
            Version = 4,
            IsDeleted = true
        };

        db.GamePatches.Add(patch);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            PatchAdminError.Stale,
            (await service.RestoreAsync(
                patch.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            PatchAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                patch.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        var deletedRevision =
            new PatchRevision
            {
                PatchId = patch.Id,
                Version = 2,
                Snapshot = "{}"
            };

        var goodRevision =
            new PatchRevision
            {
                PatchId = patch.Id,
                Version = 1,
                Snapshot = "{}"
            };

        db.PatchRevisions.AddRange(
            deletedRevision,
            goodRevision);

        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(
                new GamePatch
                {
                    Name = "Deleted snapshot",
                    IsDeleted = true
                });

        PatchAdminResult deletedSnapshot =
            await service.RestoreAsync(
                patch.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.DeletedRevision,
            deletedSnapshot.Error);

        var restoredSnapshot =
            new GamePatch
            {
                Name = "Recovered",
                Description = "Historical",
                SortOrder = 9,
                IsArchived = true,
                IsDeleted = false
            };

        journal.Setup(x => x.ReadSnapshot(
                goodRevision))
            .Returns(restoredSnapshot);

        validator.SetupSequence(x => x.ValidateAsync(
                patch.Id,
                It.IsAny<PatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conflict")
            .ReturnsAsync((string?)null);

        PatchAdminResult invalid =
            await service.RestoreAsync(
                patch.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.Validation,
            invalid.Error);

        PatchAdminResult restored =
            await service.RestoreAsync(
                patch.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            PatchAdminError.None,
            restored.Error);

        Assert.False(
            patch.IsDeleted);

        Assert.True(
            patch.IsArchived);

        Assert.Equal(
            "Recovered",
            patch.Name);

        Assert.Equal(
            "Historical",
            patch.Description);

        Assert.Equal(
            9,
            patch.SortOrder);

        Assert.Equal(
            5,
            patch.Version);

        journal.Verify(x => x.Record(
                patch,
                "restored",
                "admin"),
            Times.Once);
    }

    [Fact]
    public void MutationHelpersCoverActionsRestoreMappingAndApply()
    {
        Assert.Equal(
            "updated",
            PatchAdminService.ResolveUpdateAction(
                false,
                false));

        Assert.Equal(
            "archived",
            PatchAdminService.ResolveUpdateAction(
                false,
                true));

        Assert.Equal(
            "unarchived",
            PatchAdminService.ResolveUpdateAction(
                true,
                false));

        var snapshot =
            new GamePatch
            {
                Name = "Patch",
                Description = "Description",
                SortOrder = 9,
                IsArchived = true
            };

        PatchRequest request =
            PatchAdminService.BuildRestoreRequest(
                snapshot,
                7);

        Assert.Equal(
            "Patch",
            request.Name);

        Assert.Equal(
            "Description",
            request.Description);

        Assert.Equal(
            9,
            request.SortOrder);

        Assert.True(
            request.IsArchived);

        Assert.Equal(
            7,
            request.Version);

        var target =
            new GamePatch();

        PatchAdminService.Apply(
            target,
            new PatchRequest(
                " Name ",
                null,
                4,
                true,
                1));

        Assert.Equal(
            "Name",
            target.Name);

        Assert.Equal(
            string.Empty,
            target.Description);

        Assert.Equal(
            4,
            target.SortOrder);

        Assert.True(
            target.IsArchived);
    }

    private static PatchAdminService CreateService(
        AppDbContext db,
        IPatchAdminQueryService? queries = null,
        IPatchAdminValidator? validator = null,
        IPatchUsageGuard? usage = null,
        IPatchRevisionJournal? journal = null)
    {
        return new PatchAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ??
                new PatchAdminQueryService(db),
            validator ??
                new PatchAdminValidator(db),
            usage ??
                new PatchUsageGuard(db),
            journal ??
                new PatchRevisionJournal(db));
    }

    private static PatchRequest Request(
        string name,
        string? description = null,
        int sortOrder = 0,
        bool archived = false,
        int version = 1) =>
        new(
            name,
            description,
            sortOrder,
            archived,
            version);

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "patch-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
