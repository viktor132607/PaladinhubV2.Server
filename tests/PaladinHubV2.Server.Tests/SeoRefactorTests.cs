using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.Seo;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoRefactorTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static SeoRequest Request(string? path = "/") =>
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

    private static ContentPage Page(
        string section = "Guides",
        string slug = "guide",
        bool published = true,
        bool archived = false,
        bool deleted = false) =>
        new()
        {
            Section = section,
            Slug = slug,
            Title = $"{section} {slug}",
            JsonLayout = "[]",
            IsPublished = published,
            IsArchived = archived,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

    private static AppDbContext InMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void UrlPolicyCoversSafetyNormalizationCompositionAndHashing()
    {
        Assert.True(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "https://example.test/path"));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            new string('a', 2049)));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "https://example.test/line\nfeed"));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "https:\\example.test\\path"));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "ftp://example.test/path"));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "https://user:pass@example.test/path"));
        Assert.False(SeoUrlPolicy.IsSafeAbsoluteHttpUrl(
            "https://example.test/path#fragment"));

        Assert.Equal(string.Empty, SeoUrlPolicy.NormalizeOrigin(null));
        Assert.Equal(
            string.Empty,
            SeoUrlPolicy.NormalizeOrigin("https://user:pass@example.test"));
        Assert.Equal(
            "https://example.test",
            SeoUrlPolicy.NormalizeOrigin("https://example.test/path?q=1"));

        Assert.Equal(
            string.Empty,
            SeoUrlPolicy.CombineOriginAndPath(string.Empty, "/a"));
        Assert.Equal(
            "https://example.test/a",
            SeoUrlPolicy.CombineOriginAndPath("https://example.test/", "a"));

        var entry = new SeoPublicEntry(
            Guid.NewGuid(),
            1,
            null,
            "/",
            "Title",
            "Description",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null);
        var page = new SeoPublicPage(1, "Page", "/Guides/page");

        string first = SeoUrlPolicy.BuildSnapshotVersion(
            [entry],
            [page],
            ["/"],
            "https://site.test",
            "https://api.test");
        string second = SeoUrlPolicy.BuildSnapshotVersion(
            [entry],
            [page],
            ["/"],
            "https://site.test",
            "https://api.test");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Equal(
            SeoUrlPolicy.ResourceId("/resource"),
            SeoUrlPolicy.ResourceId("/resource"));
    }

    [Fact]
    public void RequestValidatorCoversEveryValidationRule()
    {
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { Title = new string('t', 201) }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { SocialTitle = new string('t', 201) }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { Description = new string('d', 501) }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { SocialDescription = new string('d', 501) }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with
            {
                SocialImageMediaId = Guid.NewGuid(),
                ImageUrl = "https://cdn.example.test/image.png"
            }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { CanonicalUrl = "javascript:alert(1)" }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with { ImageUrl = "javascript:alert(1)" }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request() with
            {
                ImageUrl =
                    "https://api.example.test/api/spell-icons/" +
                    Guid.NewGuid()
            }));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request("/not-configurable")));
        Assert.NotNull(SeoRequestValidator.Validate(
            Request("*") with
            {
                CanonicalUrl = "https://example.test/"
            }));

        Assert.Null(SeoRequestValidator.Validate(
            Request() with
            {
                PageId = 12,
                Path = null,
                ImageUrl = "https://cdn.example.test/image.png"
            }));
        Assert.Null(SeoService.ValidateShape(Request()));
    }

    [Fact]
    public void MapperAndRevisionFactoryCoverMappingFallbacksAndNormalization()
    {
        var entry = new SeoEntry
        {
            Id = Guid.NewGuid(),
            Version = 4,
            IsArchived = true
        };
        var request = Request() with
        {
            Title = "  Title  ",
            Description = "  Description  ",
            CanonicalUrl = "  https://example.test/path  ",
            SocialTitle = "  Social  ",
            SocialDescription = "  Social description  ",
            ImageUrl = "  https://example.test/image.png  ",
            Index = true,
            Follow = false
        };
        var target = new SeoResolvedTarget(
            null,
            "/",
            "/",
            "/",
            "static");

        SeoEntryMapper.Apply(entry, request, target);
        Assert.Equal("Title", entry.Title);
        Assert.Equal("Description", entry.Description);
        Assert.Equal("https://example.test/path", entry.CanonicalUrl);
        Assert.Equal("Social", entry.SocialTitle);
        Assert.Equal("Social description", entry.SocialDescription);
        Assert.Equal("https://example.test/image.png", entry.ImageUrl);
        Assert.True(entry.Index);
        Assert.False(entry.Follow);

        SeoSnapshot snapshot = SeoEntryMapper.ToSnapshot(entry);
        SeoRequest restored = SeoEntryMapper.FromSnapshot(snapshot, 9);
        SeoRequest current = SeoEntryMapper.ToRequest(entry, 10);
        Assert.Equal(9, restored.Version);
        Assert.Equal(10, current.Version);
        Assert.Equal(entry.Title, restored.Title);

        var page = Page();
        page.Id = 7;
        SeoAdminItem pageItem = SeoEntryMapper.ToAdminItem(
            new SeoEntry
            {
                Id = Guid.NewGuid(),
                PageId = page.Id,
                Title = "Page",
                Version = 1
            },
            new Dictionary<int, ContentPage> { [page.Id] = page });
        Assert.Equal("database", pageItem.TargetType);
        Assert.Equal("/Guides/guide", pageItem.ResolvedPath);

        SeoAdminItem unavailable = SeoEntryMapper.ToAdminItem(
            new SeoEntry
            {
                Id = Guid.NewGuid(),
                PageId = 99,
                Path = string.Empty,
                Version = 1
            },
            new Dictionary<int, ContentPage>());
        Assert.Equal("Unavailable target", unavailable.TargetLabel);

        SeoAdminItem global = SeoEntryMapper.ToAdminItem(
            new SeoEntry
            {
                Id = Guid.NewGuid(),
                Path = "*",
                Version = 1
            },
            new Dictionary<int, ContentPage>());
        Assert.Equal("global", global.TargetType);
        Assert.Equal("Global defaults", global.TargetLabel);

        SeoAdminItem nullTarget = SeoEntryMapper.ToAdminItem(
            new SeoEntry
            {
                Id = Guid.NewGuid(),
                PageId = 5,
                Path = string.Empty,
                Version = 1
            },
            (SeoResolvedTarget?)null);
        Assert.Equal("Unavailable target", nullTarget.TargetLabel);

        SeoRevision normal = SeoRevisionFactory.Create(
            entry,
            "updated",
            " editor ");
        Assert.Equal("editor", normal.Actor);
        Assert.Equal("updated", normal.Action);
        Assert.Contains("Title", normal.Snapshot);

        SeoRevision bounded = SeoRevisionFactory.Create(
            entry,
            new string('a', 40),
            new string('b', 300));
        Assert.Equal(30, bounded.Action.Length);
        Assert.Equal(256, bounded.Actor.Length);

        SeoRevision fallbackActor = SeoRevisionFactory.Create(
            entry,
            "created",
            " ");
        Assert.Equal("admin", fallbackActor.Actor);
    }

    [Fact]
    public async Task TargetResolverCoversStaticDatabaseDuplicateCollisionAndMediaRules()
    {
        await using AppDbContext db = InMemory();
        var resolver = new SeoTargetResolver(db);

        SeoResolvedTargetResult staticTarget =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request("/Holy/Overview"),
                Ct);
        Assert.Null(staticTarget.Error);
        Assert.Equal("/Holy/Overview", staticTarget.Target!.ResolvedPath);

        db.Set<SeoEntry>().Add(new SeoEntry
        {
            Id = Guid.NewGuid(),
            Path = "/Holy/Overview",
            Title = "Existing"
        });
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult duplicate =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request("/HOLY/OVERVIEW/"),
                Ct);
        Assert.Equal(409, duplicate.Status);

        SeoResolvedTargetResult missingPage =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request(null) with { PageId = 999 },
                Ct);
        Assert.Equal(409, missingPage.Status);

        ContentPage invalidPage = Page("/", "/");
        db.ContentPages.Add(invalidPage);
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult invalidPageRoute =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request(null) with { PageId = invalidPage.Id },
                Ct);
        Assert.Equal(409, invalidPageRoute.Status);

        ContentPage staticCollision = Page("Holy", "Overview");
        db.ContentPages.Add(staticCollision);
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult databaseCollision =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request(null) with { PageId = staticCollision.Id },
                Ct);
        Assert.Equal(409, databaseCollision.Status);

        db.Set<SeoEntry>().RemoveRange(db.Set<SeoEntry>());
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult staticCollisionResult =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request("/Holy/Overview"),
                Ct);
        Assert.Equal(409, staticCollisionResult.Status);

        ContentPage normalPage = Page("Guides", "resolver");
        db.ContentPages.Add(normalPage);
        await db.SaveChangesAsync(Ct);

        db.Set<SeoEntry>().Add(new SeoEntry
        {
            Id = Guid.NewGuid(),
            PageId = normalPage.Id,
            Path = string.Empty,
            Title = "Existing page SEO"
        });
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult duplicatePage =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request(null) with { PageId = normalPage.Id },
                Ct);
        Assert.Equal(409, duplicatePage.Status);

        SpellIcon inactiveMedia = new()
        {
            Id = Guid.NewGuid(),
            Name = "Inactive",
            ContentType = "image/png",
            Content = [1],
            IsArchived = true
        };
        db.SpellIcons.Add(inactiveMedia);
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult inactive =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request("/") with
                {
                    SocialImageMediaId = inactiveMedia.Id
                },
                Ct);
        Assert.Equal(409, inactive.Status);

        SpellIcon activeMedia = new()
        {
            Id = Guid.NewGuid(),
            Name = "Active",
            ContentType = "image/png",
            Content = [1]
        };
        db.SpellIcons.Add(activeMedia);
        await db.SaveChangesAsync(Ct);

        SeoResolvedTargetResult active =
            await resolver.ResolveAndValidateRelationsAsync(
                Guid.NewGuid(),
                Request("/") with
                {
                    SocialImageMediaId = activeMedia.Id
                },
                Ct);
        Assert.Equal(200, active.Status);
    }

    [Fact]
    public async Task TargetResolverCoversExistingTargetsAndPublicPathResolution()
    {
        await using AppDbContext db = InMemory();
        var resolver = new SeoTargetResolver(db);

        SeoResolvedTarget? global = await resolver.ResolveExistingTargetAsync(
            new SeoEntry { Path = "*" },
            Ct);
        Assert.Equal("global", global!.TargetType);

        SeoResolvedTarget? staticTarget =
            await resolver.ResolveExistingTargetAsync(
                new SeoEntry { Path = "/products" },
                Ct);
        Assert.Equal("static", staticTarget!.TargetType);

        ContentPage page = Page("Guides", "existing");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(Ct);

        SeoResolvedTarget? database =
            await resolver.ResolveExistingTargetAsync(
                new SeoEntry { PageId = page.Id },
                Ct);
        Assert.Equal("/Guides/existing", database!.ResolvedPath);

        SeoResolvedTarget? missing =
            await resolver.ResolveExistingTargetAsync(
                new SeoEntry { PageId = 999 },
                Ct);
        Assert.Null(missing);

        ContentPage invalid = Page("/", "/");
        db.ContentPages.Add(invalid);
        await db.SaveChangesAsync(Ct);
        Assert.Null(await resolver.ResolveExistingTargetAsync(
            new SeoEntry { PageId = invalid.Id },
            Ct));

        var pages = new Dictionary<int, ContentPage>
        {
            [page.Id] = page
        };
        Assert.Equal(
            "/Guides/existing",
            SeoTargetResolver.ResolvePublicPath(
                new SeoEntry { PageId = page.Id },
                pages));
        Assert.Null(SeoTargetResolver.ResolvePublicPath(
            new SeoEntry { PageId = 555 },
            pages));

        ContentPage collision = Page("Holy", "Overview");
        collision.Id = 123;
        pages[collision.Id] = collision;
        Assert.Null(SeoTargetResolver.ResolvePublicPath(
            new SeoEntry { PageId = collision.Id },
            pages));

        Assert.Equal(
            "*",
            SeoTargetResolver.ResolvePublicPath(
                new SeoEntry { Path = "*" },
                pages));
        Assert.Equal(
            "/products",
            SeoTargetResolver.ResolvePublicPath(
                new SeoEntry { Path = "/PRODUCTS/" },
                pages));
        Assert.Null(SeoTargetResolver.ResolvePublicPath(
            new SeoEntry { Path = "/Admin/Seo" },
            pages));
    }

    [Fact]
    public async Task PublicSnapshotServiceCoversPagesEntriesMediaAndDynamicResources()
    {
        await using AppDbContext db = InMemory();

        ContentPage normalPage = Page("Guides", "public");
        ContentPage staticCollision = Page("Holy", "Overview");
        ContentPage draft = Page("Guides", "draft", published: false);
        ContentPage invalidPage = Page("/", "/");
        db.ContentPages.AddRange(
            normalPage,
            staticCollision,
            draft,
            invalidPage);

        var product = new Product("Product", 5m)
        {
            Description = "<b>" + new string('x', 600) + "</b>"
        };
        product.Images.Add(new ProductImage
        {
            ProductId = product.Id,
            Product = product,
            Url = "https://cdn.example.test/product.png",
            SortOrder = 0
        });

        var invalidImageProduct = new Product("Invalid image", 6m);
        invalidImageProduct.Images.Add(new ProductImage
        {
            ProductId = invalidImageProduct.Id,
            Product = invalidImageProduct,
            Url = "javascript:alert(1)",
            SortOrder = 0
        });

        var invalidIdProduct = new Product
        {
            Id = "not-a-guid",
            Name = "Invalid id",
            Price = 1m
        };

        ContentPage productPage = Page("products", product.Id);
        db.ContentPages.Add(productPage);
        db.Products.AddRange(product, invalidImageProduct, invalidIdProduct);

        var activeMedia = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Active",
            ContentType = "image/png",
            Content = [1]
        };
        var inactiveMedia = new SpellIcon
        {
            Id = Guid.NewGuid(),
            Name = "Inactive",
            ContentType = "image/png",
            Content = [1],
            IsArchived = true
        };
        db.SpellIcons.AddRange(activeMedia, inactiveMedia);

        db.DiscussionPosts.Add(new DiscussionPost
        {
            Id = Guid.NewGuid(),
            Title = "Discussion",
            Content = "<p>Hello &amp; world</p>",
            AuthorId = "user-1"
        });

        await db.SaveChangesAsync(Ct);

        db.Set<SeoEntry>().AddRange(
            new SeoEntry
            {
                Path = "*",
                Title = "Global"
            },
            new SeoEntry
            {
                Path = "/",
                Title = "Home"
            },
            new SeoEntry
            {
                PageId = normalPage.Id,
                Path = string.Empty,
                Title = "Database"
            },
            new SeoEntry
            {
                PageId = staticCollision.Id,
                Path = string.Empty,
                Title = "Collision"
            },
            new SeoEntry
            {
                PageId = draft.Id,
                Path = string.Empty,
                Title = "Draft"
            },
            new SeoEntry
            {
                Path = "/Admin/Seo",
                Title = "Private"
            },
            new SeoEntry
            {
                Path = "/products",
                Title = "Media",
                SocialImageMediaId = activeMedia.Id
            },
            new SeoEntry
            {
                Path = "/privacy",
                Title = "Inactive media",
                SocialImageMediaId = inactiveMedia.Id
            },
            new SeoEntry
            {
                PageId = productPage.Id,
                Path = string.Empty,
                Title = "Custom product SEO"
            });
        await db.SaveChangesAsync(Ct);

        var service = new SeoPublicSnapshotService(db);
        SeoPublicSnapshot snapshot = await service.BuildAsync(
            "https://site.test/app",
            "https://api.test/root",
            Ct);

        Assert.Equal("https://site.test", snapshot.SiteUrl);
        Assert.Contains(snapshot.Entries, item => item.Path == "*");
        Assert.Contains(snapshot.Entries, item => item.Path == "/");
        Assert.Contains(
            snapshot.Entries,
            item => item.Path == "/Guides/public");
        Assert.DoesNotContain(
            snapshot.Entries,
            item => item.Title is "Collision" or "Draft" or "Private" or "Inactive media");
        Assert.Equal(
            $"https://api.test/api/spell-icons/{activeMedia.Id}",
            Assert.Single(
                snapshot.Entries,
                item => item.Title == "Media").ImageUrl);
        Assert.Equal(
            "https://site.test/",
            Assert.Single(
                snapshot.Entries,
                item => item.Title == "Home").CanonicalUrl);

        SeoPublicEntry customProduct = Assert.Single(
            snapshot.Entries,
            item => item.Title == "Custom product SEO");
        Assert.Equal("/products/" + product.Id, customProduct.Path);
        Assert.Single(
            snapshot.Entries,
            item => item.Path == "/products/" + product.Id);

        SeoPublicEntry invalidImageResource = Assert.Single(
            snapshot.Entries,
            item => item.Path == "/products/" + invalidImageProduct.Id);
        Assert.Equal(string.Empty, invalidImageResource.ImageUrl);

        SeoPublicEntry discussion = Assert.Single(
            snapshot.Entries,
            item => item.Path.StartsWith(
                "/Discussions/Details/",
                StringComparison.Ordinal));
        Assert.Contains("Hello & world", discussion.Description);

        Assert.DoesNotContain(
            snapshot.StaticRoutes,
            route => route.Contains("not-a-guid", StringComparison.Ordinal));
        Assert.NotEmpty(snapshot.SnapshotVersion);

        SeoPublicSnapshot noOrigins = await service.BuildAsync(
            null,
            "not-a-url",
            Ct);
        Assert.Equal(string.Empty, noOrigins.SiteUrl);
    }

    [Fact]
    public async Task ServiceInjectedDependenciesAndReadOperationsAreCovered()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();

        ContentPage first = Page("Guides", "b");
        first.Title = "B";
        ContentPage second = Page("Guides", "a");
        second.Title = "A";
        ContentPage archived = Page("Guides", "archived", archived: true);
        ContentPage deleted = Page("Guides", "deleted", deleted: true);
        ContentPage invalid = Page("/", "/");
        db.ContentPages.AddRange(first, second, archived, deleted, invalid);
        await db.SaveChangesAsync(Ct);

        var targetResolver = new Mock<ISeoTargetResolver>();
        var snapshots = new Mock<ISeoPublicSnapshotService>();
        var mutationLock = new Mock<ISeoMutationLock>();

        var expected = new SeoPublicSnapshot(
            "https://site.test",
            "registry",
            "snapshot",
            DateTime.UtcNow,
            [],
            [],
            []);
        snapshots
            .Setup(service => service.BuildAsync(
                "https://site.test",
                "https://api.test",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = new SeoService(
            db,
            new GameDataAssignmentService(db),
            targetResolver.Object,
            snapshots.Object,
            mutationLock.Object);

        Assert.Same(
            expected,
            await service.GetPublicSnapshotAsync(
                "https://site.test",
                "https://api.test",
                Ct));

        IReadOnlyList<SeoTargetPage> pages =
            await service.ListPagesAsync(Ct);
        Assert.Equal(2, pages.Count);
        Assert.Equal("A", pages[0].Title);
        Assert.Equal("B", pages[1].Title);

        var entry = new SeoEntry
        {
            Path = "/",
            Title = "History"
        };
        db.Set<SeoEntry>().Add(entry);
        db.Set<SeoRevision>().AddRange(
            new SeoRevision
            {
                EntryId = entry.Id,
                Version = 1,
                Action = "created",
                Actor = "a",
                Snapshot = "{}"
            },
            new SeoRevision
            {
                EntryId = entry.Id,
                Version = 2,
                Action = "updated",
                Actor = "b",
                Snapshot = "{}"
            });
        await db.SaveChangesAsync(Ct);

        IReadOnlyList<SeoHistoryItem> history =
            await service.HistoryAsync(entry.Id, Ct);
        Assert.Equal([2, 1], history.Select(item => item.Version).ToArray());

        IReadOnlyList<SeoAdminItem> listed = await service.ListAsync(Ct);
        Assert.Contains(listed, item => item.Id == entry.Id);
    }

    [Fact]
    public async Task ServiceSaveCoversMissingArchivedTargetErrorCreateAndUpdate()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();

        var targets = new Mock<ISeoTargetResolver>();
        var snapshots = new Mock<ISeoPublicSnapshotService>();
        var mutationLock = new Mock<ISeoMutationLock>();
        mutationLock
            .Setup(service => service.AcquireAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SeoService(
            db,
            new GameDataAssignmentService(db),
            targets.Object,
            snapshots.Object,
            mutationLock.Object);

        Assert.Equal(
            400,
            (await service.SaveAsync(
                null,
                Request() with { Title = new string('x', 201) },
                "test",
                Ct)).Status);

        Assert.Equal(
            404,
            (await service.SaveAsync(
                Guid.NewGuid(),
                Request(),
                "test",
                Ct)).Status);

        var archived = new SeoEntry
        {
            Path = "/",
            Title = "Archived",
            IsArchived = true
        };
        db.Set<SeoEntry>().Add(archived);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.SaveAsync(
                archived.Id,
                Request(),
                "test",
                Ct)).Status);

        targets
            .Setup(resolver => resolver.ResolveAndValidateRelationsAsync(
                It.IsAny<Guid>(),
                It.IsAny<SeoRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeoResolvedTargetResult(
                null,
                409,
                "Target error"));

        Assert.Equal(
            409,
            (await service.SaveAsync(
                null,
                Request(),
                "test",
                Ct)).Status);

        targets
            .Setup(resolver => resolver.ResolveAndValidateRelationsAsync(
                It.IsAny<Guid>(),
                It.IsAny<SeoRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeoResolvedTargetResult(
                new SeoResolvedTarget(
                    null,
                    "/",
                    "/",
                    "/",
                    "static"),
                200,
                null));

        SeoResult created = await service.SaveAsync(
            null,
            Request() with { Title = " Created " },
            string.Empty,
            Ct);
        Assert.Equal(201, created.Status);
        Assert.Equal("Created", created.Entry!.Title);

        SeoResult stale = await service.SaveAsync(
            created.Entry.Id,
            Request() with { Version = 0 },
            "test",
            Ct);
        Assert.Equal(409, stale.Status);

        SeoResult updated = await service.SaveAsync(
            created.Entry.Id,
            Request() with
            {
                Version = 1,
                Title = " Updated "
            },
            "editor",
            Ct);
        Assert.Equal(200, updated.Status);
        Assert.Equal(2, updated.Entry!.Version);
        Assert.Equal("Updated", updated.Entry.Title);
    }

    [Fact]
    public async Task ServiceChangeCoversFailureBranches()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();

        var targets = new Mock<ISeoTargetResolver>();
        var snapshots = new Mock<ISeoPublicSnapshotService>();
        var mutationLock = new Mock<ISeoMutationLock>();
        mutationLock
            .Setup(service => service.AcquireAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SeoService(
            db,
            new GameDataAssignmentService(db),
            targets.Object,
            snapshots.Object,
            mutationLock.Object);

        Assert.Equal(
            404,
            (await service.ChangeAsync(
                Guid.NewGuid(),
                1,
                "archive",
                null,
                "test",
                Ct)).Status);

        var entry = new SeoEntry
        {
            Path = "/",
            Title = "Entry",
            Version = 1
        };
        db.Set<SeoEntry>().Add(entry);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                2,
                "archive",
                null,
                "test",
                Ct)).Status);

        Assert.Equal(
            400,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                null,
                "test",
                Ct)).Status);

        Assert.Equal(
            404,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                Guid.NewGuid(),
                "test",
                Ct)).Status);

        var nullRevision = new SeoRevision
        {
            EntryId = entry.Id,
            Version = 50,
            Action = "test",
            Actor = "test",
            Snapshot = "null"
        };
        db.Set<SeoRevision>().Add(nullRevision);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                nullRevision.Id,
                "test",
                Ct)).Status);

        var invalidSnapshot = new SeoSnapshot(
            null,
            "/",
            new string('x', 201),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            null,
            false,
            false);
        var invalidRevision = new SeoRevision
        {
            EntryId = entry.Id,
            Version = 51,
            Action = "test",
            Actor = "test",
            Snapshot = JsonSerializer.Serialize(invalidSnapshot)
        };
        db.Set<SeoRevision>().Add(invalidRevision);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                invalidRevision.Id,
                "test",
                Ct)).Status);

        var deletedSnapshot = invalidSnapshot with
        {
            Title = "Valid",
            IsDeleted = true
        };
        var deletedRevision = new SeoRevision
        {
            EntryId = entry.Id,
            Version = 52,
            Action = "deleted",
            Actor = "test",
            Snapshot = JsonSerializer.Serialize(deletedSnapshot)
        };
        db.Set<SeoRevision>().Add(deletedRevision);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            400,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                deletedRevision.Id,
                "test",
                Ct)).Status);

        Assert.Equal(
            400,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "something-else",
                null,
                "test",
                Ct)).Status);

        entry.IsDeleted = true;
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "archive",
                null,
                "test",
                Ct)).Status);
    }

    [Fact]
    public async Task ServiceChangeCoversRestoreAndUnarchiveResolverFailures()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();

        var targets = new Mock<ISeoTargetResolver>();
        var snapshots = new Mock<ISeoPublicSnapshotService>();
        var mutationLock = new Mock<ISeoMutationLock>();
        mutationLock
            .Setup(service => service.AcquireAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SeoService(
            db,
            new GameDataAssignmentService(db),
            targets.Object,
            snapshots.Object,
            mutationLock.Object);

        var entry = new SeoEntry
        {
            Path = "/",
            Title = "Entry",
            Version = 1
        };
        db.Set<SeoEntry>().Add(entry);

        var restoreSnapshot = new SeoSnapshot(
            null,
            "/",
            "Restored",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            null,
            false,
            false);
        var revision = new SeoRevision
        {
            EntryId = entry.Id,
            Version = 10,
            Action = "created",
            Actor = "test",
            Snapshot = JsonSerializer.Serialize(restoreSnapshot)
        };
        db.Set<SeoRevision>().Add(revision);
        await db.SaveChangesAsync(Ct);

        targets
            .Setup(resolver => resolver.ResolveAndValidateRelationsAsync(
                entry.Id,
                It.IsAny<SeoRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeoResolvedTargetResult(
                null,
                409,
                "Resolver rejected"));

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "restore",
                revision.Id,
                "test",
                Ct)).Status);

        entry.IsArchived = true;
        entry.ImageUrl = "javascript:alert(1)";
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "unarchive",
                null,
                "test",
                Ct)).Status);

        entry.ImageUrl = string.Empty;
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            409,
            (await service.ChangeAsync(
                entry.Id,
                1,
                "unarchive",
                null,
                "test",
                Ct)).Status);
    }
}
