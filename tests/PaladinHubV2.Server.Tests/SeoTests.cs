using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;
using PaladinHubV2.Server.Domain.Services.Seo;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static SeoRequest Request(string path = "/") =>
        new(
            null,
            path,
            "Title",
            "Description",
            string.Empty,
            "Social title",
            "Social description",
            null,
            string.Empty,
            null,
            null,
            1);

    [Theory]
    [InlineData("/Admin/Seo")]
    [InlineData("/api/seo")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/Holy/../Admin")]
    [InlineData("/Holy/Overview?preview=true")]
    [InlineData("/Holy/Overview#fragment")]
    [InlineData("/Holy/%2e%2e/Admin")]
    [InlineData("/Holy%2fOverview")]
    [InlineData("/unknown-public-looking-route")]
    [InlineData("/Home/Home")]
    public void RejectsUnsafePrivateUnknownAndAliasStaticTargets(string path)
    {
        Assert.NotNull(SeoService.ValidateShape(Request(path)));
    }

    [Fact]
    public void StaticRegistryCanonicalizesCaseAndTrailingSlash()
    {
        Assert.True(SeoRouteRegistry.TryResolveStaticTarget(
            "/HOLY/OVERVIEW/",
            out SeoRouteDefinition? route,
            out string? error));
        Assert.Null(error);
        Assert.Equal("/Holy/Overview", route!.Route);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:pass@example.test/path")]
    [InlineData("https://example.test/path#fragment")]
    [InlineData("https:\\example.test\\path")]
    public void RejectsUnsafeCanonicalUrls(string canonical)
    {
        Assert.NotNull(SeoService.ValidateShape(Request() with { CanonicalUrl = canonical }));
    }

    [Fact]
    public void RejectsGlobalCanonical()
    {
        Assert.NotNull(SeoService.ValidateShape(Request("*") with
        {
            CanonicalUrl = "https://example.test/"
        }));
    }

    [Fact]
    public void AllowsValidExternalSocialImage()
    {
        Assert.Null(SeoService.ValidateShape(Request() with
        {
            ImageUrl = "https://cdn.example.test/images/card.png"
        }));
    }

    [Fact]
    public void RejectsMediaAndExternalImageAtTheSameTime()
    {
        Assert.NotNull(SeoService.ValidateShape(Request() with
        {
            SocialImageMediaId = Guid.NewGuid(),
            ImageUrl = "https://cdn.example.test/images/card.png"
        }));
    }

    [Fact]
    public async Task InvalidCreateDoesNotWrite()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var controller = new SeoController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestSupport.CreateHttpContext("admin-1", true)
            }
        };

        IActionResult response = await controller.Create(
            Request() with { CanonicalUrl = "javascript:alert(1)" },
            Ct);
        var result = Assert.IsType<ObjectResult>(response);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(db.Set<SeoEntry>());
        Assert.Empty(db.Set<SeoRevision>());
    }

    [Fact]
    public async Task CreateCanonicalizesStaticTargetAndRecordsActor()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);

        SeoResult result = await service.SaveAsync(
            null,
            Request("/HOLY/OVERVIEW/"),
            "admin-1",
            Ct);

        Assert.Equal(201, result.Status);
        Assert.Equal("/Holy/Overview", result.Entry!.Path);
        SeoRevision revision = await db.Set<SeoRevision>().SingleAsync(Ct);
        Assert.Equal("created", revision.Action);
        Assert.Equal("admin-1", revision.Actor);
        Assert.Equal(1, revision.Version);
    }

    [Fact]
    public async Task DuplicateStaticTargetUsesNormalizedCaseAndSlash()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);

        Assert.Equal(201, (await service.SaveAsync(
            null,
            Request("/Holy/Overview"),
            "test",
            Ct)).Status);
        Assert.Equal(409, (await service.SaveAsync(
            null,
            Request("/HOLY/OVERVIEW/"),
            "test",
            Ct)).Status);
        Assert.Single(db.Set<SeoEntry>());
        Assert.Single(db.Set<SeoRevision>());
    }

    [Fact]
    public async Task DatabasePageSeoStaysAttachedAcrossSlugRename()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var page = new ContentPage
        {
            Section = "Guides",
            Slug = "first",
            Title = "Guide",
            JsonLayout = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(Ct);

        var service = new SeoService(db);
        SeoResult created = await service.SaveAsync(
            null,
            Request() with { PageId = page.Id, Path = null },
            "test",
            Ct);
        Assert.Equal(201, created.Status);
        Assert.Equal("/Guides/first", created.Entry!.ResolvedPath);

        page.Slug = "renamed";
        page.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(Ct);

        SeoAdminItem item = Assert.Single(await service.ListAsync(Ct));
        Assert.Equal(page.Id, item.PageId);
        Assert.Equal("/Guides/renamed", item.ResolvedPath);
    }

    [Fact]
    public async Task DatabasePageCannotCollideWithStaticRoute()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var page = new ContentPage
        {
            Section = "Holy",
            Slug = "Overview",
            Title = "Collision",
            JsonLayout = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(Ct);

        SeoResult result = await new SeoService(db).SaveAsync(
            null,
            Request() with { PageId = page.Id, Path = null },
            "test",
            Ct);
        Assert.Equal(409, result.Status);
        Assert.Empty(db.Set<SeoEntry>());
    }

    [Fact]
    public async Task StaticRouteCannotCollideWithDatabasePage()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        db.ContentPages.Add(new ContentPage
        {
            Section = "Holy",
            Slug = "Overview",
            Title = "Collision",
            JsonLayout = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        });
        await db.SaveChangesAsync(Ct);

        SeoResult result = await new SeoService(db).SaveAsync(
            null,
            Request("/Holy/Overview"),
            "test",
            Ct);
        Assert.Equal(409, result.Status);
    }

    [Fact]
    public async Task RejectsArchivedMediaReference()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var media = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Archived",
            ContentType = "image/png",
            Content = [1],
            IsArchived = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        SeoResult result = await new SeoService(db).SaveAsync(
            null,
            Request() with { SocialImageMediaId = media.Id },
            "test",
            Ct);
        Assert.Equal(409, result.Status);
        Assert.Empty(db.Set<SeoEntry>());
    }

    [Fact]
    public async Task ActiveMediaReferenceAppearsAsAbsoluteSnapshotImage()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var media = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Social",
            ContentType = "image/png",
            Content = [1],
            CreatedAtUtc = DateTime.UtcNow
        };
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        var service = new SeoService(db);
        Assert.Equal(201, (await service.SaveAsync(
            null,
            Request() with { SocialImageMediaId = media.Id },
            "test",
            Ct)).Status);

        SeoPublicSnapshot snapshot = await service.GetPublicSnapshotAsync(
            "https://www.example.test",
            "https://api.example.test",
            Ct);
        SeoPublicEntry entry = Assert.Single(snapshot.Entries);
        Assert.Equal($"https://api.example.test/api/spell-icons/{media.Id}", entry.ImageUrl);
        Assert.Equal("https://www.example.test/", entry.CanonicalUrl);
    }

    [Fact]
    public async Task MediaReferencedBySeoCannotBeArchivedOrDeletedThroughMediaService()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var media = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Social",
            ContentType = "image/png",
            Content = [1],
            CreatedAtUtc = DateTime.UtcNow
        };
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);
        await new SeoService(db).SaveAsync(
            null,
            Request() with { SocialImageMediaId = media.Id },
            "test",
            Ct);

        var assignments = new GameDataAssignmentService(db);
        var mediaService = new MediaAdminService(db, assignments);
        MediaAdminResult archive = await mediaService.UpdateAsync(
            media.Id,
            new MediaRequest("Social", "", "", true, media.Version),
            "test",
            Ct);
        Assert.Equal(MediaAdminError.InUse, archive.Error);

        MediaAdminResult delete = await mediaService.DeleteAsync(
            media.Id,
            media.Version,
            "test",
            Ct);
        Assert.Equal(MediaAdminError.InUse, delete.Error);
        Assert.False(media.IsArchived);
        Assert.False(media.IsDeleted);
    }

    [Fact]
    public async Task StaleUpdateDoesNotChangeEntryOrAddRevision()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        SeoAdminItem created = (await service.SaveAsync(
            null,
            Request(),
            "test",
            Ct)).Entry!;

        SeoResult result = await service.SaveAsync(
            created.Id,
            Request() with { Title = "Wrong", Version = 0 },
            "test",
            Ct);

        Assert.Equal(409, result.Status);
        Assert.Equal("Title", (await db.Set<SeoEntry>().SingleAsync(Ct)).Title);
        Assert.Single(db.Set<SeoRevision>());
    }

    [Fact]
    public async Task ArchiveAndUnarchiveRecordVersions()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        SeoAdminItem created = (await service.SaveAsync(null, Request(), "creator", Ct)).Entry!;

        SeoResult archived = await service.ChangeAsync(
            created.Id,
            1,
            "archive",
            null,
            "archiver",
            Ct);
        Assert.Equal(200, archived.Status);
        Assert.True(archived.Entry!.IsArchived);
        Assert.Equal(2, archived.Entry.Version);

        SeoResult unarchived = await service.ChangeAsync(
            created.Id,
            2,
            "unarchive",
            null,
            "restorer",
            Ct);
        Assert.Equal(200, unarchived.Status);
        Assert.False(unarchived.Entry!.IsArchived);
        Assert.Equal(3, unarchived.Entry.Version);

        Assert.Equal(
            ["created", "archive", "unarchive"],
            await db.Set<SeoRevision>()
                .OrderBy(item => item.Version)
                .Select(item => item.Action)
                .ToArrayAsync(Ct));
    }

    [Fact]
    public async Task UnarchiveRevalidatesMediaActivity()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var media = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Social",
            ContentType = "image/png",
            Content = [1],
            CreatedAtUtc = DateTime.UtcNow
        };
        db.SpellIcons.Add(media);
        await db.SaveChangesAsync(Ct);

        var service = new SeoService(db);
        SeoAdminItem created = (await service.SaveAsync(
            null,
            Request() with { SocialImageMediaId = media.Id },
            "test",
            Ct)).Entry!;
        await service.ChangeAsync(created.Id, 1, "archive", null, "test", Ct);

        media.IsArchived = true;
        media.Version++;
        await db.SaveChangesAsync(Ct);

        SeoResult result = await service.ChangeAsync(
            created.Id,
            2,
            "unarchive",
            null,
            "test",
            Ct);
        Assert.Equal(409, result.Status);
        SeoEntry entry = await db.Set<SeoEntry>().SingleAsync(Ct);
        Assert.True(entry.IsArchived);
        Assert.Equal(2, entry.Version);
    }

    [Fact]
    public async Task DeleteCanOnlyRestoreANonDeletedRevision()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        SeoAdminItem created = (await service.SaveAsync(null, Request(), "creator", Ct)).Entry!;
        SeoRevision original = await db.Set<SeoRevision>().SingleAsync(Ct);

        Assert.Equal(200, (await service.ChangeAsync(
            created.Id,
            1,
            "delete",
            null,
            "deleter",
            Ct)).Status);
        SeoRevision deleted = await db.Set<SeoRevision>()
            .SingleAsync(item => item.Version == 2, Ct);

        Assert.Equal(400, (await service.ChangeAsync(
            created.Id,
            2,
            "restore",
            deleted.Id,
            "test",
            Ct)).Status);

        SeoResult restored = await service.ChangeAsync(
            created.Id,
            2,
            "restore",
            original.Id,
            "restorer",
            Ct);
        Assert.Equal(200, restored.Status);
        Assert.False(restored.Entry!.IsDeleted);
        Assert.False(restored.Entry.IsArchived);
        Assert.Equal(3, restored.Entry.Version);
    }

    [Fact]
    public async Task RestoreRevalidatesDuplicateTarget()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        SeoAdminItem first = (await service.SaveAsync(
            null,
            Request("/Holy/Overview"),
            "test",
            Ct)).Entry!;
        SeoRevision original = await db.Set<SeoRevision>()
            .SingleAsync(item => item.EntryId == first.Id, Ct);
        await service.ChangeAsync(first.Id, 1, "delete", null, "test", Ct);

        Assert.Equal(201, (await service.SaveAsync(
            null,
            Request("/Holy/Overview"),
            "test",
            Ct)).Status);

        SeoResult restore = await service.ChangeAsync(
            first.Id,
            2,
            "restore",
            original.Id,
            "test",
            Ct);
        Assert.Equal(409, restore.Status);
    }

    [Fact]
    public async Task PublicSnapshotFiltersLifecycleInvalidRoutesAndUnpublishedPages()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        var published = new ContentPage
        {
            Section = "Guides",
            Slug = "published",
            Title = "Published",
            JsonLayout = "[]",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        var draft = new ContentPage
        {
            Section = "Guides",
            Slug = "draft",
            Title = "Draft",
            JsonLayout = "[]",
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        db.ContentPages.AddRange(published, draft);
        await db.SaveChangesAsync(Ct);

        var service = new SeoService(db);
        await service.SaveAsync(null, Request("*"), "test", Ct);
        await service.SaveAsync(null, Request("/products"), "test", Ct);
        await service.SaveAsync(null, Request() with { PageId = published.Id, Path = null }, "test", Ct);
        await service.SaveAsync(null, Request() with { PageId = draft.Id, Path = null }, "test", Ct);

        db.Set<SeoEntry>().AddRange(
            new SeoEntry { Path = "/Admin/Seo", Title = "Invalid manual row" },
            new SeoEntry { Path = "/privacy", Title = "Archived", IsArchived = true },
            new SeoEntry { Path = "/Holy/Gear", Title = "Deleted", IsDeleted = true });
        await db.SaveChangesAsync(Ct);

        SeoPublicSnapshot snapshot = await service.GetPublicSnapshotAsync(
            "https://www.example.test/",
            "https://api.example.test/",
            Ct);

        Assert.Contains(snapshot.Entries, item => item.Path == "*");
        Assert.Contains(snapshot.Entries, item => item.Path == "/products");
        Assert.Contains(snapshot.Entries, item => item.PageId == published.Id);
        Assert.DoesNotContain(snapshot.Entries, item => item.PageId == draft.Id);
        Assert.DoesNotContain(snapshot.Entries, item => item.Path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot.Entries, item => item.Title == "Archived" || item.Title == "Deleted");
        Assert.DoesNotContain(snapshot.Entries, item => item.GetType().GetProperty("Actor") is not null);
    }

    [Fact]
    public async Task PublicControllerDoesNotExposeRevisionHistory()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        await new SeoService(db).SaveAsync(null, Request(), "secret-actor", Ct);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClientApp:BaseUrl"] = "https://www.example.test",
                ["Api:PublicBaseUrl"] = "https://api.example.test"
            })
            .Build();
        var controller = new PublicSeoController(db, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Snapshot(Ct));
        string json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("secret-actor", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Snapshot", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsDeleted", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IsArchived", json, StringComparison.Ordinal);
    }
}
