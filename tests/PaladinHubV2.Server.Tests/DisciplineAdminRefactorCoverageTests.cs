using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class DisciplineAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsUsageAndHistory()
    {
        await using AppDbContext db = CreateDb();

        var paladin = new GameDiscipline
        {
            Name = "Paladin",
            Description = "Class",
            SortOrder = 2
        };
        var holy = new GameDiscipline
        {
            Name = "Holy",
            ParentId = 1,
            SortOrder = 1
        };

        db.GameDisciplines.AddRange(paladin, holy);
        await db.SaveChangesAsync(Ct);

        holy.ParentId = paladin.Id;
        db.Spells.Add(new Spell
        {
            Name = "Flash",
            DisciplineId = paladin.Id
        });
        db.Items.Add(new Item
        {
            Name = "Hammer",
            DisciplineId = paladin.Id
        });
        db.DisciplineRevisions.AddRange(
            new DisciplineRevision
            {
                DisciplineId = paladin.Id,
                Version = 1,
                Action = "created"
            },
            new DisciplineRevision
            {
                DisciplineId = paladin.Id,
                Version = 3,
                Action = "updated"
            });
        await db.SaveChangesAsync(Ct);

        var service =
            new DisciplineAdminQueryService(db);

        List<DisciplineListItem> rows =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Holy", "Paladin"],
            rows.Select(row => row.Name));
        DisciplineListItem row =
            rows.Single(item => item.Id == paladin.Id);
        Assert.Equal(2, row.UsageCount);
        Assert.Equal(1, row.ChildCount);

        List<DisciplineRevision> history =
            await service.HistoryAsync(
                paladin.Id,
                Ct);
        Assert.Equal(
            [3, 1],
            history.Select(item => item.Version));
    }

    [Fact]
    public async Task ValidatorCoversAllHierarchyRules()
    {
        await using AppDbContext db = CreateDb();

        var paladin = new GameDiscipline
        {
            Name = "Paladin",
            SortOrder = 1
        };
        var archived = new GameDiscipline
        {
            Name = "Archived",
            IsArchived = true,
            SortOrder = 2
        };
        db.GameDisciplines.AddRange(paladin, archived);
        await db.SaveChangesAsync(Ct);

        var holy = new GameDiscipline
        {
            Name = "Holy",
            ParentId = paladin.Id,
            SortOrder = 1
        };
        var deleted = new GameDiscipline
        {
            Name = "Deleted",
            IsDeleted = true,
            SortOrder = 3
        };
        db.GameDisciplines.AddRange(holy, deleted);
        await db.SaveChangesAsync(Ct);

        var validator =
            new DisciplineAdminValidator(db);

        Assert.Equal(
            "Name is required.",
            await validator.ValidateAsync(
                0,
                Request(" "),
                Ct));

        Assert.Equal(
            "A class or specialization with this name already exists under this class.",
            await validator.ValidateAsync(
                0,
                Request(" paladin "),
                Ct));

        Assert.Equal(
            "A class cannot belong to itself or its specializations.",
            await validator.ValidateAsync(
                paladin.Id,
                Request(
                    "Paladin updated",
                    paladin.Id,
                    version: paladin.Version),
                Ct));

        Assert.Equal(
            "A specialization must belong to a top-level class.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Missing parent",
                    99999),
                Ct));

        Assert.Equal(
            "A specialization must belong to a top-level class.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Nested",
                    holy.Id),
                Ct));

        Assert.Equal(
            "A class with specializations cannot become a specialization.",
            await validator.ValidateAsync(
                paladin.Id,
                Request(
                    "Paladin moved",
                    archived.Id,
                    version: paladin.Version),
                Ct));

        Assert.Equal(
            "An active specialization cannot belong to an archived class.",
            await validator.ValidateAsync(
                0,
                Request(
                    "Active child",
                    archived.Id,
                    archived: false),
                Ct));

        Assert.Equal(
            "Archive or move active specializations first.",
            await validator.ValidateAsync(
                paladin.Id,
                Request(
                    "Paladin",
                    archived: true,
                    version: paladin.Version),
                Ct));

        holy.IsArchived = true;
        await db.SaveChangesAsync(Ct);

        Assert.Null(
            await validator.ValidateAsync(
                paladin.Id,
                Request(
                    " Paladin Prime ",
                    archived: true,
                    version: paladin.Version),
                Ct));
    }

    [Fact]
    public async Task UsageGuardChecksChildrenSpellsItemsAndUnused()
    {
        await using AppDbContext db = CreateDb();

        var parent = new GameDiscipline
        {
            Name = "Parent"
        };
        db.GameDisciplines.Add(parent);
        await db.SaveChangesAsync(Ct);

        var child = new GameDiscipline
        {
            Name = "Child",
            ParentId = parent.Id
        };
        db.GameDisciplines.Add(child);
        await db.SaveChangesAsync(Ct);

        var guard = new DisciplineUsageGuard(db);

        Assert.True(
            await guard.IsInUseAsync(
                parent.Id,
                Ct));

        child.IsDeleted = true;
        db.Spells.Add(new Spell
        {
            Name = "Spell",
            DisciplineId = parent.Id
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
            DisciplineId = parent.Id
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
        var discipline = new GameDiscipline
        {
            Name = "Paladin",
            Description = "Class",
            Version = 4
        };
        db.GameDisciplines.Add(discipline);
        await db.SaveChangesAsync(Ct);

        var journal =
            new DisciplineRevisionJournal(db);

        journal.Record(
            discipline,
            "updated",
            "admin");
        await db.SaveChangesAsync(Ct);

        DisciplineRevision revision =
            await db.DisciplineRevisions.SingleAsync(Ct);

        Assert.Equal(discipline.Id, revision.DisciplineId);
        Assert.Equal(4, revision.Version);
        Assert.Equal("updated", revision.Action);
        Assert.Equal("admin", revision.Actor);

        GameDiscipline snapshot =
            journal.ReadSnapshot(revision);
        Assert.Equal("Paladin", snapshot.Name);
        Assert.Equal(4, snapshot.Version);
    }

    [Fact]
    public async Task FacadeForwardsQueriesAndCreatesWithValidation()
    {
        await using AppDbContext db = CreateDb();
        var queries =
            new Mock<IDisciplineAdminQueryService>();
        var validator =
            new Mock<IDisciplineAdminValidator>();
        var usage =
            new Mock<IDisciplineUsageGuard>();
        var journal =
            new Mock<IDisciplineRevisionJournal>();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new DisciplineListItem(
                    1,
                    "Paladin",
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
                [new DisciplineRevision
                {
                    DisciplineId = 1,
                    Version = 1
                }]);

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<int>(),
                It.IsAny<DisciplineRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad")
            .ReturnsAsync((string?)null);

        Assert.NotNull(
            new DisciplineAdminService(
                db,
                new GameDataAssignmentService(db)));

        DisciplineAdminService service =
            CreateService(
                db,
                queries.Object,
                validator.Object,
                usage.Object,
                journal.Object);

        Assert.Single(await service.ListAsync(Ct));
        Assert.Single(await service.HistoryAsync(1, Ct));

        DisciplineAdminResult invalid =
            await service.CreateAsync(
                Request("Bad"),
                "admin",
                Ct);
        Assert.Equal(
            DisciplineAdminError.Validation,
            invalid.Error);
        Assert.Equal("bad", invalid.Message);

        DisciplineAdminResult created =
            await service.CreateAsync(
                Request(
                    " Paladin ",
                    description: " class "),
                "admin",
                Ct);

        Assert.Equal(
            DisciplineAdminError.None,
            created.Error);
        Assert.NotNull(created.Discipline);
        Assert.Equal(
            "Paladin",
            created.Discipline!.Name);
        Assert.Equal(
            "class",
            created.Discipline.Description);

        journal.Verify(x => x.Record(
            created.Discipline,
            "created",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleValidationAndSuccess()
    {
        await using AppDbContext db = CreateDb();
        var validator =
            new Mock<IDisciplineAdminValidator>();
        var usage =
            new Mock<IDisciplineUsageGuard>();
        var journal =
            new Mock<IDisciplineRevisionJournal>();

        DisciplineAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            DisciplineAdminError.NotFound,
            (await service.UpdateAsync(
                99,
                Request("Missing"),
                "admin",
                Ct)).Error);

        var discipline = new GameDiscipline
        {
            Name = "Paladin",
            Version = 2
        };
        db.GameDisciplines.Add(discipline);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            DisciplineAdminError.Stale,
            (await service.UpdateAsync(
                discipline.Id,
                Request(
                    "Paladin",
                    version: 1),
                "admin",
                Ct)).Error);

        validator.SetupSequence(x => x.ValidateAsync(
                discipline.Id,
                It.IsAny<DisciplineRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid")
            .ReturnsAsync((string?)null);

        DisciplineAdminResult invalid =
            await service.UpdateAsync(
                discipline.Id,
                Request(
                    "Paladin",
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            DisciplineAdminError.Validation,
            invalid.Error);

        DisciplineAdminResult updated =
            await service.UpdateAsync(
                discipline.Id,
                Request(
                    " Paladin Prime ",
                    description: " updated ",
                    archived: true,
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            DisciplineAdminError.None,
            updated.Error);
        Assert.Equal(3, discipline.Version);
        Assert.True(discipline.IsArchived);
        Assert.Equal(
            "Paladin Prime",
            discipline.Name);

        journal.Verify(x => x.Record(
            discipline,
            "archived",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db = CreateDb();
        var usage =
            new Mock<IDisciplineUsageGuard>();
        var journal =
            new Mock<IDisciplineRevisionJournal>();

        DisciplineAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            DisciplineAdminError.NotFound,
            (await service.DeleteAsync(
                99,
                1,
                "admin",
                Ct)).Error);

        var discipline = new GameDiscipline
        {
            Name = "Paladin",
            Version = 2
        };
        db.GameDisciplines.Add(discipline);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            DisciplineAdminError.Stale,
            (await service.DeleteAsync(
                discipline.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.IsInUseAsync(
                discipline.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        DisciplineAdminResult inUse =
            await service.DeleteAsync(
                discipline.Id,
                2,
                "admin",
                Ct);
        Assert.Equal(
            DisciplineAdminError.InUse,
            inUse.Error);
        Assert.NotNull(inUse.Message);

        DisciplineAdminResult deleted =
            await service.DeleteAsync(
                discipline.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            DisciplineAdminError.None,
            deleted.Error);
        Assert.True(discipline.IsDeleted);
        Assert.Equal(3, discipline.Version);

        journal.Verify(x => x.Record(
            discipline,
            "deleted",
            "admin"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversAllFailureModesAndSuccess()
    {
        await using AppDbContext db = CreateDb();
        var validator =
            new Mock<IDisciplineAdminValidator>();
        var journal =
            new Mock<IDisciplineRevisionJournal>();

        DisciplineAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            DisciplineAdminError.NotFound,
            (await service.RestoreAsync(
                99,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        var discipline = new GameDiscipline
        {
            Name = "Current",
            Version = 4,
            IsDeleted = true
        };
        db.GameDisciplines.Add(discipline);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            DisciplineAdminError.Stale,
            (await service.RestoreAsync(
                discipline.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            DisciplineAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                discipline.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        var deletedRevision =
            new DisciplineRevision
            {
                DisciplineId = discipline.Id,
                Version = 2,
                Snapshot = "{}"
            };
        var goodRevision =
            new DisciplineRevision
            {
                DisciplineId = discipline.Id,
                Version = 1,
                Snapshot = "{}"
            };
        db.DisciplineRevisions.AddRange(
            deletedRevision,
            goodRevision);
        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(new GameDiscipline
            {
                Name = "Deleted snapshot",
                IsDeleted = true
            });

        DisciplineAdminResult deletedSnapshot =
            await service.RestoreAsync(
                discipline.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);
        Assert.Equal(
            DisciplineAdminError.DeletedRevision,
            deletedSnapshot.Error);

        var restoredSnapshot =
            new GameDiscipline
            {
                Name = "Paladin",
                Description = "Restored",
                SortOrder = 7,
                IsArchived = true,
                IsDeleted = false
            };

        journal.Setup(x => x.ReadSnapshot(
                goodRevision))
            .Returns(restoredSnapshot);

        validator.SetupSequence(x => x.ValidateAsync(
                discipline.Id,
                It.IsAny<DisciplineRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conflict")
            .ReturnsAsync((string?)null);

        DisciplineAdminResult invalid =
            await service.RestoreAsync(
                discipline.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);
        Assert.Equal(
            DisciplineAdminError.Validation,
            invalid.Error);

        DisciplineAdminResult restored =
            await service.RestoreAsync(
                discipline.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            DisciplineAdminError.None,
            restored.Error);
        Assert.False(discipline.IsDeleted);
        Assert.True(discipline.IsArchived);
        Assert.Equal("Paladin", discipline.Name);
        Assert.Equal(5, discipline.Version);

        journal.Verify(x => x.Record(
            discipline,
            "restored",
            "admin"),
            Times.Once);
    }

    [Fact]
    public void MutationHelpersCoverUpdateActionsRestoreMappingAndApply()
    {
        Assert.Equal(
            "updated",
            DisciplineAdminService.ResolveUpdateAction(
                false,
                false));
        Assert.Equal(
            "archived",
            DisciplineAdminService.ResolveUpdateAction(
                false,
                true));
        Assert.Equal(
            "unarchived",
            DisciplineAdminService.ResolveUpdateAction(
                true,
                false));

        var snapshot = new GameDiscipline
        {
            Name = "Paladin",
            Description = "Class",
            ParentId = 3,
            SortOrder = 9,
            IsArchived = true
        };

        DisciplineRequest request =
            DisciplineAdminService.BuildRestoreRequest(
                snapshot,
                7);

        Assert.Equal("Paladin", request.Name);
        Assert.Equal("Class", request.Description);
        Assert.Equal(3, request.ParentId);
        Assert.Equal(9, request.SortOrder);
        Assert.True(request.IsArchived);
        Assert.Equal(7, request.Version);

        var target = new GameDiscipline();
        DisciplineAdminService.Apply(
            target,
            new DisciplineRequest(
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

    private static DisciplineAdminService CreateService(
        AppDbContext db,
        IDisciplineAdminQueryService? queries = null,
        IDisciplineAdminValidator? validator = null,
        IDisciplineUsageGuard? usage = null,
        IDisciplineRevisionJournal? journal = null)
    {
        return new DisciplineAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ?? new DisciplineAdminQueryService(db),
            validator ?? new DisciplineAdminValidator(db),
            usage ?? new DisciplineUsageGuard(db),
            journal ?? new DisciplineRevisionJournal(db));
    }

    private static DisciplineRequest Request(
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
                    "discipline-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
