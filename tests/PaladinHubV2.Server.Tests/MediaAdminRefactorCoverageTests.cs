using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.Tests;

public sealed class MediaAdminRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceCoversStatusesSearchPagingUsageAndHistory()
    {
        await using AppDbContext db = CreateDb();

        DateTime now = DateTime.UtcNow;

        SpellIcon activeA = Media(
            "Alpha",
            archived: false,
            deleted: false,
            created: now);

        SpellIcon activeB = Media(
            "Beta",
            archived: false,
            deleted: false,
            created: now.AddMinutes(-1));

        SpellIcon archived = Media(
            "Archived",
            archived: true,
            deleted: false,
            created: now.AddMinutes(-2));

        SpellIcon deleted = Media(
            "Deleted",
            archived: false,
            deleted: true,
            created: now.AddMinutes(-3));

        db.SpellIcons.AddRange(
            activeA,
            activeB,
            archived,
            deleted);

        db.MediaRevisions.AddRange(
            new MediaRevision
            {
                MediaId = activeA.Id,
                Version = 1,
                Action = "uploaded",
                Snapshot = "{}"
            },
            new MediaRevision
            {
                MediaId = activeA.Id,
                Version = 3,
                Action = "updated",
                Snapshot = "{}"
            });

        await db.SaveChangesAsync(Ct);

        var usage = new Mock<IMediaUsageCounter>();

        usage.Setup(x => x.CountAsync(
                activeA.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        usage.Setup(x => x.CountAsync(
                activeB.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        usage.Setup(x => x.CountAsync(
                archived.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        usage.Setup(x => x.CountAsync(
                deleted.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service =
            new MediaAdminQueryService(
                db,
                usage.Object);

        MediaPageResult activePage =
            await service.ListAsync(
                null,
                "active",
                0,
                999,
                Ct);

        Assert.Equal(2, activePage.Total);
        Assert.Equal(1, activePage.Page);
        Assert.Equal(1, activePage.Pages);
        Assert.Equal(
            ["Alpha", "Beta"],
            activePage.Media.Select(x => x.Name));
        Assert.Equal(7, activePage.Media[0].UsageCount);
        Assert.Equal(
            "/api/spell-icons/" + activeA.Id,
            activePage.Media[0].Icon);
        Assert.Equal(3, activePage.Media[0].Size);

        MediaPageResult archivedPage =
            await service.ListAsync(
                null,
                "archived",
                1,
                32,
                Ct);

        Assert.Single(archivedPage.Media);
        Assert.Equal(
            "Archived",
            archivedPage.Media[0].Name);

        MediaPageResult deletedPage =
            await service.ListAsync(
                null,
                "deleted",
                1,
                32,
                Ct);

        Assert.Single(deletedPage.Media);
        Assert.Equal(
            "Deleted",
            deletedPage.Media[0].Name);

        MediaPageResult allPage =
            await service.ListAsync(
                " alpha ",
                "all",
                99,
                1,
                Ct);

        Assert.Single(allPage.Media);
        Assert.Equal(
            "Alpha",
            allPage.Media[0].Name);
        Assert.Equal(1, allPage.Page);

        List<MediaRevision> history =
            await service.HistoryAsync(
                activeA.Id,
                Ct);

        Assert.Equal(
            [3, 1],
            history.Select(x => x.Version));
    }

    [Fact]
    public void ValidatorCoversBlankAndValidNames()
    {
        var validator =
            new MediaAdminValidator();

        Assert.Equal(
            "Name is required.",
            validator.Validate(
                new MediaRequest(
                    " ",
                    null,
                    null,
                    false,
                    1)));

        Assert.Null(
            validator.Validate(
                new MediaRequest(
                    "image.png",
                    null,
                    null,
                    false,
                    1)));
    }

    [Fact]
    public async Task UsageCounterAggregatesDatabaseAndBannerReferences()
    {
        await using AppDbContext db = CreateDb();

        Guid id = Guid.NewGuid();
        string key = id.ToString();

        db.Spells.Add(
            new Spell
            {
                Name = "Spell",
                Icon = "/api/spell-icons/" + key
            });

        db.Items.Add(
            new Item
            {
                Name = "Item",
                SecondIcon =
                    "/api/spell-icons/" + key
            });

        db.ContentPages.Add(
            new ContentPage
            {
                Section = "holy",
                Slug = "guide",
                Title = "Guide",
                JsonLayout =
                    "{\"image\":\"/api/spell-icons/" +
                    key +
                    "\"}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        db.Set<SeoEntry>().AddRange(
            new SeoEntry
            {
                Path = "/one",
                SocialImageMediaId = id
            },
            new SeoEntry
            {
                Path = "/two",
                ImageUrl =
                    "/api/spell-icons/" + key
            },
            new SeoEntry
            {
                Path = "/deleted",
                SocialImageMediaId = id,
                IsDeleted = true
            });

        await db.SaveChangesAsync(Ct);

        var banners =
            new Mock<IMediaBannerUsageLookup>();

        banners.Setup(x => x.CountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var counter =
            new MediaUsageCounter(
                db,
                banners.Object);

        int count =
            await counter.CountAsync(
                id,
                Ct);

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task BannerUsageLookupReturnsZeroOutsidePostgres()
    {
        await using AppDbContext db = CreateDb();

        var lookup =
            new MediaBannerUsageLookup(db);

        Assert.Equal(
            0,
            await lookup.CountAsync(
                Guid.NewGuid(),
                Ct));
    }

    [Fact]
    public async Task RevisionJournalRecordsAndReadsSnapshotIncludingNull()
    {
        await using AppDbContext db = CreateDb();

        SpellIcon media = Media(
            "Original",
            archived: true,
            deleted: false,
            created: DateTime.UtcNow);

        media.AltText = "Alt";
        media.Description = "Description";
        media.Version = 8;

        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        var journal =
            new MediaRevisionJournal(db);

        journal.Record(
            media,
            "archived",
            "admin");

        await db.SaveChangesAsync(Ct);

        MediaRevision revision =
            await db.MediaRevisions.SingleAsync(Ct);

        Assert.Equal(media.Id, revision.MediaId);
        Assert.Equal(8, revision.Version);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("admin", revision.Actor);

        MediaSnapshot? snapshot =
            journal.ReadSnapshot(revision);

        Assert.NotNull(snapshot);
        Assert.Equal("Original", snapshot!.Name);
        Assert.Equal("Alt", snapshot.AltText);
        Assert.Equal(
            "Description",
            snapshot.Description);
        Assert.True(snapshot.IsArchived);
        Assert.False(snapshot.IsDeleted);

        Assert.Null(
            journal.ReadSnapshot(
                new MediaRevision
                {
                    Snapshot = "null"
                }));
    }

    [Fact]
    public async Task FacadeForwardsQueriesAndValidatesBeforeMutation()
    {
        await using AppDbContext db = CreateDb();

        var queries =
            new Mock<IMediaAdminQueryService>();

        var validator =
            new Mock<IMediaAdminValidator>();

        var usage =
            new Mock<IMediaUsageCounter>();

        var journal =
            new Mock<IMediaRevisionJournal>();

        Guid id = Guid.NewGuid();

        queries.Setup(x => x.ListAsync(
                "x",
                "all",
                2,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MediaPageResult(
                    [],
                    1,
                    1,
                    0));

        queries.Setup(x => x.HistoryAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        validator.Setup(x => x.Validate(
                It.IsAny<MediaRequest>()))
            .Returns("invalid");

        Assert.NotNull(
            new MediaAdminService(
                db,
                new GameDataAssignmentService(db)));

        MediaAdminService service =
            CreateService(
                db,
                queries.Object,
                validator.Object,
                usage.Object,
                journal.Object);

        Assert.Equal(
            0,
            (await service.ListAsync(
                "x",
                "all",
                2,
                10,
                Ct)).Total);

        Assert.Empty(
            await service.HistoryAsync(
                id,
                Ct));

        MediaAdminResult invalid =
            await service.UpdateAsync(
                id,
                Request(),
                "admin",
                Ct);

        Assert.Equal(
            MediaAdminError.Validation,
            invalid.Error);
        Assert.Equal(
            "invalid",
            invalid.Message);
    }

    [Fact]
    public async Task UpdateCoversNotFoundStaleInUseAndSuccessActions()
    {
        await using AppDbContext db = CreateDb();

        var validator =
            new Mock<IMediaAdminValidator>();

        var usage =
            new Mock<IMediaUsageCounter>();

        var journal =
            new Mock<IMediaRevisionJournal>();

        validator.Setup(x => x.Validate(
                It.IsAny<MediaRequest>()))
            .Returns((string?)null);

        MediaAdminService service =
            CreateService(
                db,
                validator: validator.Object,
                usage: usage.Object,
                journal: journal.Object);

        Guid missingId = Guid.NewGuid();

        Assert.Equal(
            MediaAdminError.NotFound,
            (await service.UpdateAsync(
                missingId,
                Request(),
                "admin",
                Ct)).Error);

        SpellIcon media =
            Media(
                "image.png",
                false,
                false,
                DateTime.UtcNow);

        media.Version = 3;
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            MediaAdminError.Stale,
            (await service.UpdateAsync(
                media.Id,
                Request(version: 2),
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.CountAsync(
                media.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ReturnsAsync(0);

        MediaAdminResult inUse =
            await service.UpdateAsync(
                media.Id,
                Request(
                    archived: true,
                    version: 3),
                "admin",
                Ct);

        Assert.Equal(
            MediaAdminError.InUse,
            inUse.Error);

        MediaAdminResult archived =
            await service.UpdateAsync(
                media.Id,
                new MediaRequest(
                    " Renamed ",
                    " Alt ",
                    " Desc ",
                    true,
                    3),
                "editor",
                Ct);

        Assert.Equal(
            MediaAdminError.None,
            archived.Error);
        Assert.Equal(media.Id, archived.Id);
        Assert.Equal("Renamed", media.Name);
        Assert.Equal("Alt", media.AltText);
        Assert.Equal("Desc", media.Description);
        Assert.True(media.IsArchived);
        Assert.Equal(4, media.Version);

        journal.Verify(x => x.Record(
            media,
            "archived",
            "editor"),
            Times.Once);

        MediaAdminResult updated =
            await service.UpdateAsync(
                media.Id,
                new MediaRequest(
                    "Still archived",
                    null,
                    null,
                    true,
                    4),
                "editor",
                Ct);

        Assert.Equal(
            MediaAdminError.None,
            updated.Error);
        Assert.Equal(string.Empty, media.AltText);
        Assert.Equal(
            string.Empty,
            media.Description);

        journal.Verify(x => x.Record(
            media,
            "updated",
            "editor"),
            Times.Once);

        MediaAdminResult unarchived =
            await service.UpdateAsync(
                media.Id,
                Request(
                    archived: false,
                    version: 5),
                "editor",
                Ct);

        Assert.Equal(
            MediaAdminError.None,
            unarchived.Error);

        journal.Verify(x => x.Record(
            media,
            "unarchived",
            "editor"),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCoversNotFoundStaleInUseAndSuccess()
    {
        await using AppDbContext db = CreateDb();

        var usage =
            new Mock<IMediaUsageCounter>();

        var journal =
            new Mock<IMediaRevisionJournal>();

        MediaAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            MediaAdminError.NotFound,
            (await service.DeleteAsync(
                Guid.NewGuid(),
                1,
                "admin",
                Ct)).Error);

        SpellIcon media =
            Media(
                "image.png",
                false,
                false,
                DateTime.UtcNow);

        media.Version = 2;
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            MediaAdminError.Stale,
            (await service.DeleteAsync(
                media.Id,
                1,
                "admin",
                Ct)).Error);

        usage.SetupSequence(x => x.CountAsync(
                media.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2)
            .ReturnsAsync(0);

        MediaAdminResult inUse =
            await service.DeleteAsync(
                media.Id,
                2,
                "admin",
                Ct);

        Assert.Equal(
            MediaAdminError.InUse,
            inUse.Error);

        MediaAdminResult deleted =
            await service.DeleteAsync(
                media.Id,
                2,
                "deleter",
                Ct);

        Assert.Equal(
            MediaAdminError.None,
            deleted.Error);
        Assert.Null(deleted.Id);
        Assert.True(media.IsDeleted);
        Assert.Equal(3, media.Version);

        journal.Verify(x => x.Record(
            media,
            "deleted",
            "deleter"),
            Times.Once);
    }

    [Fact]
    public async Task RestoreCoversEveryFailureAndSuccessPath()
    {
        await using AppDbContext db = CreateDb();

        var usage =
            new Mock<IMediaUsageCounter>();

        var journal =
            new Mock<IMediaRevisionJournal>();

        MediaAdminService service =
            CreateService(
                db,
                usage: usage.Object,
                journal: journal.Object);

        Assert.Equal(
            MediaAdminError.NotFound,
            (await service.RestoreAsync(
                Guid.NewGuid(),
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    1),
                "admin",
                Ct)).Error);

        SpellIcon media =
            Media(
                "deleted.png",
                false,
                true,
                DateTime.UtcNow);

        media.Version = 4;
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            MediaAdminError.Stale,
            (await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    3),
                "admin",
                Ct)).Error);

        Assert.Equal(
            MediaAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    Guid.NewGuid(),
                    4),
                "admin",
                Ct)).Error);

        MediaRevision nullRevision =
            new()
            {
                MediaId = media.Id,
                Version = 1,
                Snapshot = "null"
            };

        MediaRevision deletedRevision =
            new()
            {
                MediaId = media.Id,
                Version = 2,
                Snapshot = "{}"
            };

        MediaRevision archivedRevision =
            new()
            {
                MediaId = media.Id,
                Version = 3,
                Snapshot = "{}"
            };

        db.MediaRevisions.AddRange(
            nullRevision,
            deletedRevision,
            archivedRevision);

        await db.SaveChangesAsync(Ct);

        journal.Setup(x => x.ReadSnapshot(
                nullRevision))
            .Returns((MediaSnapshot?)null);

        Assert.Equal(
            MediaAdminError.RevisionNotFound,
            (await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    nullRevision.Id,
                    4),
                "admin",
                Ct)).Error);

        journal.Setup(x => x.ReadSnapshot(
                deletedRevision))
            .Returns(
                new MediaSnapshot(
                    "Old",
                    "",
                    "",
                    false,
                    true));

        MediaAdminResult deletedSnapshot =
            await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    deletedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            MediaAdminError.DeletedRevision,
            deletedSnapshot.Error);

        journal.Setup(x => x.ReadSnapshot(
                archivedRevision))
            .Returns(
                new MediaSnapshot(
                    "Archived",
                    "Alt",
                    "Description",
                    true,
                    false));

        usage.SetupSequence(x => x.CountAsync(
                media.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ReturnsAsync(0);

        MediaAdminResult archivedInUse =
            await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    archivedRevision.Id,
                    4),
                "admin",
                Ct);

        Assert.Equal(
            MediaAdminError.InUse,
            archivedInUse.Error);

        MediaAdminResult restored =
            await service.RestoreAsync(
                media.Id,
                new RevisionRestoreRequest(
                    archivedRevision.Id,
                    4),
                "restorer",
                Ct);

        Assert.Equal(
            MediaAdminError.None,
            restored.Error);
        Assert.Equal(media.Id, restored.Id);
        Assert.Equal("Archived", media.Name);
        Assert.Equal("Alt", media.AltText);
        Assert.Equal(
            "Description",
            media.Description);
        Assert.True(media.IsArchived);
        Assert.False(media.IsDeleted);
        Assert.Equal(5, media.Version);

        journal.Verify(x => x.Record(
            media,
            "restored",
            "restorer"),
            Times.Once);
    }

    [Fact]
    public void HelpersCoverArchiveChecksActionsAndMapping()
    {
        Assert.True(
            MediaAdminService
                .ShouldCheckUsageBeforeArchive(
                    false,
                    true));

        Assert.False(
            MediaAdminService
                .ShouldCheckUsageBeforeArchive(
                    true,
                    true));

        Assert.Equal(
            "updated",
            MediaAdminService
                .ResolveUpdateAction(
                    false,
                    false));

        Assert.Equal(
            "archived",
            MediaAdminService
                .ResolveUpdateAction(
                    false,
                    true));

        Assert.Equal(
            "unarchived",
            MediaAdminService
                .ResolveUpdateAction(
                    true,
                    false));

        SpellIcon media =
            Media(
                "before",
                false,
                false,
                DateTime.UtcNow);

        MediaAdminService.Apply(
            media,
            new MediaRequest(
                " Name ",
                null,
                null,
                true,
                1));

        Assert.Equal("Name", media.Name);
        Assert.Equal(string.Empty, media.AltText);
        Assert.Equal(
            string.Empty,
            media.Description);
        Assert.True(media.IsArchived);

        MediaAdminService.ApplySnapshot(
            media,
            new MediaSnapshot(
                "Historic",
                "Historical alt",
                "Historical description",
                false,
                false));

        Assert.Equal("Historic", media.Name);
        Assert.Equal(
            "Historical alt",
            media.AltText);
        Assert.Equal(
            "Historical description",
            media.Description);
        Assert.False(media.IsArchived);
    }

    private static MediaAdminService CreateService(
        AppDbContext db,
        IMediaAdminQueryService? queries = null,
        IMediaAdminValidator? validator = null,
        IMediaUsageCounter? usage = null,
        IMediaRevisionJournal? journal = null)
    {
        return new MediaAdminService(
            db,
            new GameDataAssignmentService(db),
            queries ??
                Mock.Of<IMediaAdminQueryService>(),
            validator ??
                new MediaAdminValidator(),
            usage ??
                Mock.Of<IMediaUsageCounter>(),
            journal ??
                Mock.Of<IMediaRevisionJournal>());
    }

    private static MediaRequest Request(
        bool archived = false,
        int version = 1)
    {
        return new MediaRequest(
            "image.png",
            "alt",
            "description",
            archived,
            version);
    }

    private static SpellIcon Media(
        string name,
        bool archived,
        bool deleted,
        DateTime created)
    {
        return new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = name,
            AltText = name + " alt",
            Description = name + " desc",
            ContentType = "image/png",
            Content = [1, 2, 3],
            IsArchived = archived,
            IsDeleted = deleted,
            Version = 1,
            CreatedAtUtc = created
        };
    }

    private static AppDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    "media-refactor-" +
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new AppDbContext(options);
    }
}
