using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class TagAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsUsageAndHistory()
    {
        await using AppDbContext db = CreateDb();

        var beta = new GameTag
        {
            Name = "Beta",
            Description = "Beta tag",
            SortOrder = 2
        };
        var alpha = new GameTag
        {
            Name = "Alpha",
            SortOrder = 1
        };

        db.GameTags.AddRange(beta, alpha);
        await db.SaveChangesAsync(Ct);

        db.Spells.Add(new Spell
        {
            Name = "Spell",
            TagIds = [beta.Id]
        });
        db.Items.Add(new Item
        {
            Name = "Item",
            TagIds = [beta.Id]
        });
        db.TagRevisions.AddRange(
            new TagRevision
            {
                TagId = beta.Id,
                Version = 1,
                Action = "created"
            },
            new TagRevision
            {
                TagId = beta.Id,
                Version = 4,
                Action = "updated"
            });

        await db.SaveChangesAsync(Ct);

        var service =
            new TagAdminQueryService(db);

        List<TagListItem> rows =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Alpha", "Beta"],
            rows.Select(row => row.Name));

        TagListItem row =
            rows.Single(item => item.Id == beta.Id);

        Assert.Equal(2, row.UsageCount);

        List<TagRevision> history =
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

        var active = new GameTag
        {
            Name = "Dragonflight"
        };
        var deleted = new GameTag
        {
            Name = "Deleted",
            IsDeleted = true
        };

        db.GameTags.AddRange(
            active,
            deleted);

        await db.SaveChangesAsync(Ct);

        var validator =
            new TagAdminValidator(db);

        Assert.Equal(
            "Name is required.",
            await validator.ValidateAsync(
                0,
                Request(" "),
                Ct));

        Assert.Equal(
            "A tag with this name already exists.",
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

        var tag = new GameTag
        {
            Name = "Tag"
        };

        db.GameTags.Add(tag);
        await db.SaveChangesAsync(Ct);

        var guard =
            new TagUsageGuard(db);

        Assert.False(
            await guard.IsInUseAsync(
                tag.Id,
                Ct));

        var spell = new Spell
        {
            Name = "Spell",
            TagIds = [tag.Id]
        };
        db.Spells.Add(spell);
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                tag.Id,
                Ct));

        db.Spells.Remove(spell);

        var item = new Item
        {
            Name = "Item",
            TagIds = [tag.Id]
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await guard.IsInUseAsync(
                tag.Id,
                Ct));

        db.Items.Remove(item);
        await db.SaveChangesAsync(Ct);

        Assert.False(
            await guard.IsInUseAsync(
                tag.Id,
                Ct));
    }

    [Fact]
    public async Task RevisionJournalRecordsAndReadsSnapshots()
    {
        await using AppDbContext db = CreateDb();

        var tag = new GameTag
        {
            Name = "Tag",
            Description = "Description",
            Version = 5
        };

        db.GameTags.Add(tag);
        await db.SaveChangesAsync(Ct);

        var journal =
            new TagRevisionJournal(db);

        journal.Record(
            tag,
            "updated",
            "admin");

        await db.SaveChangesAsync(Ct);

        TagRevision revision =
            await db.TagRevisions.SingleAsync(Ct);

        Assert.Equal(
            tag.Id,
            revision.TagId);

        Assert.Equal(
            5,
            revision.Version);

        Assert.Equal(
            "updated",
            revision.Action);

        Assert.Equal(
            "admin",
            revision.Actor);

        GameTag snapshot =
            journal.ReadSnapshot(revision);

        Assert.Equal(
            "Tag",
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
            new Mock<ITagAdminQueryService>();

        var validator =
            new Mock<ITagAdminValidator>();

        var usage =
            new Mock<ITagUsageGuard>();

        var journal =
            new Mock<ITagRevisionJournal>();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new TagListItem(
                    1,
                    "Tag",
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
                [new TagRevision
                {
                    TagId = 1,
                    Version = 1
                }]);

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<int>(),
                It.IsAny<TagRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad")
            .ReturnsAsync((string?)null);

        Assert.NotNull(
            new TagAdminService(
                db,
                new GameDataAssignmentService(db)));

        TagAdminService service =
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

        TagAdminResult invalid =
            await service.CreateAsync(
                Request("Bad"),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.Validation,
            invalid.Error);

        Assert.Equal(
            "bad",
            invalid.Message);

        TagAdminResult created =
            await service.CreateAsync(
                Request(
                    " Tag 11.0 ",
                    " launch ",
                    7),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.None,
            created.Error);

        Assert.NotNull(
            created.Tag);

        Assert.Equal(
            "Tag 11.0",
            created.Tag!.Name);

        Assert.Equal(
            "launch",
            created.Tag.Description);

        journal.Verify(x => x.Record(
                created.Tag,
                "created",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleValidationAndAllActions()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<ITagAdminValidator>();

        var journal =
            new Mock<ITagRevisionJournal>();

        TagAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            TagAdminError.NotFound,
            (await service.UpdateAsync(
                999,
                Request("Missing"),
                "admin",
                Ct)).Error);

        var tag = new GameTag
        {
            Name = "Old",
            Version = 2
        };

        db.GameTags.Add(tag);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            TagAdminError.Stale,
            (await service.UpdateAsync(
                tag.Id,
                Request(
                    "Old",
                    version: 1),
                "admin",
                Ct)).Error);

        validator.SetupSequence(x => x.ValidateAsync(
                tag.Id,
                It.IsAny<TagRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid")
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null);

        TagAdminResult invalid =
            await service.UpdateAsync(
                tag.Id,
                Request(
                    "Old",
                    version: 2),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.Validation,
            invalid.Error);

        TagAdminResult archived =
            await service.UpdateAsync(
                tag.Id,
                Request(
                    " New ",
                    " changed ",
                    7,
                    true,
                    2),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.None,
            archived.Error);

        Assert.Equal(
            3,
            tag.Version);

        Assert.True(
            tag.IsArchived);

        Assert.Equal(
            "New",
            tag.Name);

        Assert.Equal(
            "changed",
            tag.Description);

        Assert.Equal(
            7,
            tag.SortOrder);

        journal.Verify(x => x.Record(
                tag,
                "archived",
                "admin"),
            Times.Once);

        TagAdminResult unarchived =
            await service.UpdateAsync(
                tag.Id,
                Request(
                    "New",
                    "changed",
                    7,
                    false,
                    3),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.None,
            unarchived.Error);

        Assert.False(
            tag.IsArchived);

        journal.Verify(x => x.Record(
                tag,
                "unarchived",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var usage =
            new Mock<ITagUsageGuard>();

        var journal =
            new Mock<ITagRevisionJournal>();

        TagAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            TagAdminError.NotFound,
            (await service.DeleteAsync(
                999,
                1,
                "admin",
                Ct)).Error);

        var tag = new GameTag
        {
            Name = "Tag",
            Version = 2
        };

        db.GameTags.Add(tag);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            TagAdminError.Stale,
            (await service.DeleteAsync(
                tag.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.IsInUseAsync(
                tag.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        TagAdminResult inUse =
            await service.DeleteAsync(
                tag.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.InUse,
            inUse.Error);

        Assert.Contains(
            "Remove this tag",
            inUse.Message);

        TagAdminResult deleted =
            await service.DeleteAsync(
                tag.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.None,
            deleted.Error);

        Assert.True(
            tag.IsDeleted);

        Assert.Equal(
            3,
            tag.Version);

        journal.Verify(x => x.Record(
                tag,
                "deleted",
                "admin"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversAllFailureModesAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<ITagAdminValidator>();

        var journal =
            new Mock<ITagRevisionJournal>();

        TagAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                journal: journal.Object);

        Assert.Equal(
            TagAdminError.NotFound,
            (await service.RestoreAsync(
                999,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        var tag = new GameTag
        {
            Name = "Current",
            Version = 4,
            IsDeleted = true
        };

        db.GameTags.Add(tag);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            TagAdminError.Stale,
            (await service.RestoreAsync(
                tag.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            TagAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                tag.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        var deletedRevision =
            new TagRevision
            {
                TagId = tag.Id,
                Version = 2,
                Snapshot = "{}"
            };

        var goodRevision =
            new TagRevision
            {
                TagId = tag.Id,
                Version = 1,
                Snapshot = "{}"
            };

        db.TagRevisions.AddRange(
            deletedRevision,
            goodRevision);

        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(
                new GameTag
                {
                    Name = "Deleted snapshot",
                    IsDeleted = true
                });

        TagAdminResult deletedSnapshot =
            await service.RestoreAsync(
                tag.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.DeletedRevision,
            deletedSnapshot.Error);

        var restoredSnapshot =
            new GameTag
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
                tag.Id,
                It.IsAny<TagRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conflict")
            .ReturnsAsync((string?)null);

        TagAdminResult invalid =
            await service.RestoreAsync(
                tag.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.Validation,
            invalid.Error);

        TagAdminResult restored =
            await service.RestoreAsync(
                tag.Id,
                new RevisionRestoreRequest(
                    goodRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            TagAdminError.None,
            restored.Error);

        Assert.False(
            tag.IsDeleted);

        Assert.True(
            tag.IsArchived);

        Assert.Equal(
            "Recovered",
            tag.Name);

        Assert.Equal(
            "Historical",
            tag.Description);

        Assert.Equal(
            9,
            tag.SortOrder);

        Assert.Equal(
            5,
            tag.Version);

        journal.Verify(x => x.Record(
                tag,
                "restored",
                "admin"),
            Times.Once);
    }

    [Fact]
    public void MutationHelpersCoverActionsRestoreMappingAndApply()
    {
        Assert.Equal(
            "updated",
            TagAdminService.ResolveUpdateAction(
                false,
                false));

        Assert.Equal(
            "archived",
            TagAdminService.ResolveUpdateAction(
                false,
                true));

        Assert.Equal(
            "unarchived",
            TagAdminService.ResolveUpdateAction(
                true,
                false));

        var snapshot =
            new GameTag
            {
                Name = "Tag",
                Description = "Description",
                SortOrder = 9,
                IsArchived = true
            };

        TagRequest request =
            TagAdminService.BuildRestoreRequest(
                snapshot,
                7);

        Assert.Equal(
            "Tag",
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
            new GameTag();

        TagAdminService.Apply(
            target,
            new TagRequest(
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

    private static TagAdminService CreateService(
        AppDbContext db,
        ITagAdminQueryService? queries = null,
        ITagAdminValidator? validator = null,
        ITagUsageGuard? usage = null,
        ITagRevisionJournal? journal = null)
    {
        return new TagAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ??
                new TagAdminQueryService(db),
            validator ??
                new TagAdminValidator(db),
            usage ??
                new TagUsageGuard(db),
            journal ??
                new TagRevisionJournal(db));
    }

    private static TagRequest Request(
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
                    "tag-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
