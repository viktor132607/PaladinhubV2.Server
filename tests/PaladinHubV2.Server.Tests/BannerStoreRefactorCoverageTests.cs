using Moq;
using PaladinHubV2.Server.Domain.Services.Banners;

namespace PaladinHubV2.Server.Tests;

public sealed class BannerStoreRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public void RulesNormalizePathsAndValidateAllPolicies()
    {
        var rules = new BannerRules();

        Assert.Equal("/", rules.NormalizePath(null));
        Assert.Equal("/", rules.NormalizePath("  "));
        Assert.Equal("/holy/overview", rules.NormalizePath(" holy/overview/ "));
        Assert.Equal("/holy/overview", rules.NormalizePath("/holy/overview///"));

        BannerRequest valid = Request();

        Assert.Null(rules.Validate(valid, true));
        Assert.Null(rules.Validate(valid, false));

        Assert.Contains("Internal name", rules.Validate(valid with { InternalName = " " }, true));
        Assert.Contains("Internal name", rules.Validate(valid with { InternalName = new string('x', 101) }, true));
        Assert.Contains("Title", rules.Validate(valid with { Title = "" }, true));
        Assert.Contains("Title", rules.Validate(valid with { Title = new string('x', 201) }, true));
        Assert.Contains("Text", rules.Validate(valid with { Text = "" }, true));
        Assert.Contains("Text", rules.Validate(valid with { Text = new string('x', 10001) }, true));
        Assert.Contains("maximum length", rules.Validate(valid with { AltText = new string('x', 301) }, true));
        Assert.Contains("maximum length", rules.Validate(valid with { ButtonText = new string('x', 121), ButtonUrl = "/go" }, true));
        Assert.Contains("maximum length", rules.Validate(valid with { ImageUrl = new string('x', 2049) }, true));
        Assert.Contains("maximum length", rules.Validate(valid with { ButtonText = "Open", ButtonUrl = new string('x', 2049) }, true));
        Assert.Contains("Kind", rules.Validate(valid with { Kind = "danger" }, true));
        Assert.Contains("Position", rules.Validate(valid with { Position = "footer" }, true));

        DateTimeOffset start = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.Contains(
            "End date",
            rules.Validate(
                valid with
                {
                    StartAtUtc = start,
                    EndAtUtc = start.AddSeconds(-1)
                },
                true));

        Assert.Contains(
            "provided together",
            rules.Validate(
                valid with
                {
                    ButtonText = "Open",
                    ButtonUrl = null
                },
                true));

        Assert.Contains(
            "provided together",
            rules.Validate(
                valid with
                {
                    ButtonText = null,
                    ButtonUrl = "/go"
                },
                true));

        foreach (string unsafeUrl in new[]
                 {
                     "javascript:alert(1)",
                     "//evil.test",
                     "/\\evil.test",
                     "https://safe.test\n/path"
                 })
        {
            Assert.Contains(
                "Button URL",
                rules.Validate(
                    valid with
                    {
                        ButtonText = "Open",
                        ButtonUrl = unsafeUrl
                    },
                    true));
        }

        Assert.Contains(
            "Image URL",
            rules.Validate(
                valid with
                {
                    ImageUrl = "ftp://unsafe.test/image.png"
                },
                true));

        Assert.Null(
            rules.Validate(
                valid with
                {
                    ButtonText = "Open",
                    ButtonUrl = "https://safe.test/path",
                    ImageUrl = "/api/spell-icons/image.png"
                },
                true));

        Assert.Contains(
            "at most 200",
            rules.Validate(
                valid with
                {
                    Pages = Enumerable.Range(0, 201)
                        .Select(index => $"/page-{index}")
                        .ToArray()
                },
                true));

        foreach (string invalidScope in new[]
                 {
                     "",
                     "//evil",
                     "/bad\\scope",
                     "/bad\u0001scope",
                     "/page?query=1",
                     "/page#fragment",
                     "/Admin/Database",
                     "/api/auth/me",
                     "/Account/Profile",
                     "/Checkout",
                     "/cart",
                     "/login",
                     "/register"
                 })
        {
            Assert.Contains(
                "Page scopes",
                rules.Validate(
                    valid with
                    {
                        Pages = [invalidScope]
                    },
                    true));
        }

        Assert.Contains(
            "Version is required.",
            rules.Validate(
                valid with { Version = 0 },
                false));

        Assert.Null(
            rules.Validate(
                valid with
                {
                    Kind = " SUCCESS ",
                    Position = " BELOW-NAVBAR ",
                    ButtonText = "Open",
                    ButtonUrl = "/go",
                    Pages = ["/Holy/Overview"]
                },
                true));
    }

    [Fact]
    public void RulesCreateUpdateMapAndVisibility()
    {
        var rules = new BannerRules();
        DateTimeOffset now =
            new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        BannerRequest request =
            Request() with
            {
                InternalName = " notice ",
                Title = " Title ",
                Text = " Body ",
                ImageUrl = " /image.png ",
                AltText = " Alt ",
                ButtonText = " Open ",
                ButtonUrl = " /go ",
                Kind = " WARNING ",
                Position = " ABOVE-CONTENT ",
                Pages =
                [
                    "/z/",
                    " /a/ ",
                    "/A",
                    " "
                ],
                StartAtUtc = now.AddMinutes(-1),
                EndAtUtc = now.AddMinutes(2),
                SortOrder = 4,
                IsDismissible = false,
                IsActive = true
            };

        BannerDto created =
            rules.Create(request, now);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("notice", created.InternalName);
        Assert.Equal("Title", created.Title);
        Assert.Equal("Body", created.Text);
        Assert.Equal("/image.png", created.ImageUrl);
        Assert.Equal("Alt", created.AltText);
        Assert.Equal("Open", created.ButtonText);
        Assert.Equal("/go", created.ButtonUrl);
        Assert.Equal("warning", created.Kind);
        Assert.Equal("above-content", created.Position);
        Assert.Equal(new[] { "/a", "/z" }, created.Pages);
        Assert.Equal(1, created.Version);
        Assert.Equal(now, created.CreatedAtUtc);
        Assert.Equal(now, created.UpdatedAtUtc);

        BannerDto noOptional =
            rules.Create(
                Request() with
                {
                    ImageUrl = " ",
                    AltText = null,
                    ButtonText = null,
                    ButtonUrl = null,
                    Pages = null
                },
                now);

        Assert.Null(noOptional.ImageUrl);
        Assert.Equal(string.Empty, noOptional.AltText);
        Assert.Null(noOptional.ButtonText);
        Assert.Null(noOptional.ButtonUrl);
        Assert.Empty(noOptional.Pages);

        DateTimeOffset later = now.AddHours(1);

        BannerDto updated =
            rules.Update(
                created,
                Request() with
                {
                    InternalName = " next ",
                    Title = " Next title ",
                    Text = " Next body ",
                    ImageUrl = null,
                    AltText = null,
                    ButtonText = null,
                    ButtonUrl = null,
                    Kind = "success",
                    Position = "below-navbar",
                    Pages = ["/b/"],
                    StartAtUtc = null,
                    EndAtUtc = null,
                    SortOrder = 9,
                    IsDismissible = true,
                    IsActive = false
                },
                later);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("next", updated.InternalName);
        Assert.Equal("Next title", updated.Title);
        Assert.Equal("Next body", updated.Text);
        Assert.Null(updated.ImageUrl);
        Assert.Equal(string.Empty, updated.AltText);
        Assert.Equal("success", updated.Kind);
        Assert.Equal("below-navbar", updated.Position);
        Assert.Equal(new[] { "/b" }, updated.Pages);
        Assert.Equal(2, updated.Version);
        Assert.Equal(later, updated.UpdatedAtUtc);

        BannerRequest roundTrip =
            rules.ToRequest(updated, 17);

        Assert.Equal(updated.InternalName, roundTrip.InternalName);
        Assert.Equal(updated.Title, roundTrip.Title);
        Assert.Equal(updated.Text, roundTrip.Text);
        Assert.Equal(updated.Pages, roundTrip.Pages);
        Assert.Equal(17, roundTrip.Version);

        BannerDto visible =
            created with
            {
                Pages = ["/Holy/Overview"],
                StartAtUtc = now,
                EndAtUtc = now.AddMinutes(1),
                IsActive = true,
                IsArchived = false,
                IsDeleted = false
            };

        Assert.True(rules.IsVisible(visible, "/holy/overview/", now));
        Assert.False(rules.IsVisible(visible, "/holy/overview", now.AddTicks(-1)));
        Assert.False(rules.IsVisible(visible, "/holy/overview", now.AddMinutes(1)));
        Assert.False(rules.IsVisible(visible, "/protection/overview", now));
        Assert.False(rules.IsVisible(visible with { IsArchived = true }, "/holy/overview", now));
        Assert.False(rules.IsVisible(visible with { IsDeleted = true }, "/holy/overview", now));
        Assert.False(rules.IsVisible(visible with { IsActive = false }, "/holy/overview", now));
        Assert.True(rules.IsVisible(visible with { Pages = [] }, "/any", now));
        Assert.True(rules.IsVisible(visible with { StartAtUtc = null, EndAtUtc = null }, "/holy/overview", now));
    }

    [Fact]
    public async Task ServiceListsFiltersSortsAndForwardsHistory()
    {
        var repository = new Mock<IBannerRepository>();
        var rules = new BannerRules();
        var time = new FixedTimeProvider(UtcNow());

        BannerDto active = Banner("b-active", "below-navbar", 2, active: true);
        BannerDto matching = Banner("a-match", "above-content", 1, active: true) with { Title = "Needle" };
        BannerDto inactive = Banner("inactive", "above-content", 0, active: false);
        BannerDto archived = Banner("archived", "above-navbar", 0, active: true) with { IsArchived = true };
        BannerDto deleted = Banner("deleted", "above-navbar", 0, active: true) with { IsDeleted = true };

        repository.Setup(x => x.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([active, matching, inactive, archived, deleted]);

        repository.Setup(x => x.HistoryAsync(
                active.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new BannerRevisionDto(
                    Guid.NewGuid(),
                    active.Id,
                    2,
                    "updated",
                    "admin",
                    UtcNow(),
                    active)
            ]);

        var service = new BannerStoreService(repository.Object, rules, time);

        IReadOnlyList<BannerDto> defaultItems =
            await service.ListAsync(null, "active", Ct);

        Assert.Equal(new[] { "a-match", "b-active" }, defaultItems.Select(x => x.InternalName));

        Assert.Single(await service.ListAsync("needle", "all", Ct));
        Assert.Single(await service.ListAsync(null, "inactive", Ct));
        Assert.Single(await service.ListAsync(null, "archived", Ct));
        Assert.Single(await service.ListAsync(null, "deleted", Ct));
        Assert.Equal(5, (await service.ListAsync(null, "ALL", Ct)).Count);

        Assert.Single(await service.HistoryAsync(active.Id, Ct));
    }

    [Fact]
    public async Task ServiceCreateValidatesBeforeRepositoryAndCreatesNormalizedBanner()
    {
        var repository = new Mock<IBannerRepository>();
        var rules = new BannerRules();
        DateTimeOffset now = UtcNow();
        var service = new BannerStoreService(
            repository.Object,
            rules,
            new FixedTimeProvider(now));

        BannerStoreResult invalid =
            await service.CreateAsync(
                Request() with { Title = "" },
                "admin",
                Ct);

        Assert.Equal(400, invalid.Status);
        Assert.Equal("banner.validation", invalid.Code);

        repository.Verify(
            x => x.CreateAsync(
                It.IsAny<BannerDto>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Setup(x => x.CreateAsync(
                It.IsAny<BannerDto>(),
                "creator",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto banner, string _, CancellationToken _) =>
                new BannerStoreResult(200, "ok", "", banner));

        BannerStoreResult created =
            await service.CreateAsync(
                Request() with
                {
                    InternalName = " notice ",
                    Title = " title "
                },
                "creator",
                Ct);

        Assert.Equal(200, created.Status);
        Assert.NotNull(created.Banner);
        Assert.Equal("notice", created.Banner!.InternalName);
        Assert.Equal("title", created.Banner.Title);
        Assert.Equal(now, created.Banner.CreatedAtUtc);
    }

    [Fact]
    public async Task ServiceUpdateCoversValidationLookupStateConcurrencyAndSuccess()
    {
        var repository = new Mock<IBannerRepository>();
        var service = new BannerStoreService(
            repository.Object,
            new BannerRules(),
            new FixedTimeProvider(UtcNow()));

        Guid id = Guid.NewGuid();

        Assert.Equal(
            "banner.validation",
            (await service.UpdateAsync(
                id,
                Request() with { Version = 0 },
                "admin",
                Ct)).Code);

        repository.SetupSequence(x => x.FindAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto?)null)
            .ReturnsAsync(Banner("deleted", "above-content", 0, true) with { IsDeleted = true })
            .ReturnsAsync(Banner("archived", "above-content", 0, true) with { IsArchived = true })
            .ReturnsAsync(Banner("stale", "above-content", 0, true) with { Version = 3 })
            .ReturnsAsync(Banner("ok", "above-content", 0, true) with { Version = 1 });

        Assert.Equal(404, (await service.UpdateAsync(id, Request(), "admin", Ct)).Status);
        Assert.Equal(404, (await service.UpdateAsync(id, Request(), "admin", Ct)).Status);
        Assert.Equal("banner.archived", (await service.UpdateAsync(id, Request(), "admin", Ct)).Code);
        Assert.Equal("banner.stale", (await service.UpdateAsync(id, Request(), "admin", Ct)).Code);

        repository.Setup(x => x.ReplaceAsync(
                It.IsAny<BannerDto>(),
                It.IsAny<BannerDto>(),
                "updated",
                "editor",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto _, BannerDto next, string _, string _, CancellationToken _) =>
                new BannerStoreResult(200, "ok", "", next));

        BannerStoreResult updated =
            await service.UpdateAsync(
                id,
                Request() with
                {
                    InternalName = "changed",
                    Version = 1
                },
                "editor",
                Ct);

        Assert.Equal(200, updated.Status);
        Assert.Equal("changed", updated.Banner!.InternalName);
        Assert.Equal(2, updated.Banner.Version);
    }

    [Fact]
    public async Task ServiceChangeCoversEveryActionAndRestoreFailure()
    {
        var repository = new Mock<IBannerRepository>();
        var rules = new Mock<IBannerRules>();
        DateTimeOffset now = UtcNow();
        var service = new BannerStoreService(
            repository.Object,
            rules.Object,
            new FixedTimeProvider(now));

        Guid id = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();

        repository.Setup(x => x.FindAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto?)null);

        Assert.Equal(
            "banner.notFound",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(1, "archive", null),
                "admin",
                Ct)).Code);

        BannerDto current = Banner("current", "above-content", 0, true) with { Id = id, Version = 4 };

        repository.Setup(x => x.FindAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        Assert.Equal(
            "banner.stale",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(3, "archive", null),
                "admin",
                Ct)).Code);

        repository.Setup(x => x.FindAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current with { IsDeleted = true });

        Assert.Equal(
            "banner.deleted",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "archive", null),
                "admin",
                Ct)).Code);

        repository.Setup(x => x.FindAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        Assert.Equal(
            "banner.revisionRequired",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "restore", null),
                "admin",
                Ct)).Code);

        repository.Setup(x => x.RevisionSnapshotAsync(
                id,
                revisionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto?)null);

        Assert.Equal(
            "banner.revisionNotFound",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "restore", revisionId),
                "admin",
                Ct)).Code);

        BannerDto deletedSnapshot =
            Banner("snapshot", "above-content", 0, true) with { IsDeleted = true };

        repository.Setup(x => x.RevisionSnapshotAsync(
                id,
                revisionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedSnapshot);

        Assert.Equal(
            "banner.deletedRevision",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "restore", revisionId),
                "admin",
                Ct)).Code);

        BannerDto snapshot =
            Banner("snapshot", "above-content", 0, true);

        repository.Setup(x => x.RevisionSnapshotAsync(
                id,
                revisionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        rules.Setup(x => x.ToRequest(snapshot, 4))
            .Returns(Request() with { Version = 4 });

        rules.SetupSequence(x => x.Validate(
                It.IsAny<BannerRequest>(),
                false))
            .Returns("invalid restore")
            .Returns((string?)null);

        Assert.Equal(
            "banner.restoreInvalid",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "restore", revisionId),
                "admin",
                Ct)).Code);

        repository.Setup(x => x.ReplaceAsync(
                current,
                It.IsAny<BannerDto>(),
                It.IsAny<string>(),
                "admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BannerDto _, BannerDto next, string _, string _, CancellationToken _) =>
                new BannerStoreResult(200, "ok", "", next));

        BannerStoreResult restored =
            await service.ChangeAsync(
                id,
                new BannerActionRequest(4, " RESTORE ", revisionId),
                "admin",
                Ct);

        Assert.Equal(200, restored.Status);
        Assert.False(restored.Banner!.IsDeleted);
        Assert.Equal(5, restored.Banner.Version);
        Assert.Equal(now, restored.Banner.UpdatedAtUtc);

        rules.Setup(x => x.Validate(
                It.IsAny<BannerRequest>(),
                false))
            .Returns((string?)null);

        foreach ((string action, string expected, Action<BannerDto> assert) in new[]
                 {
                     ("archive", "archived", (Action<BannerDto>)(next => Assert.True(next.IsArchived))),
                     ("unarchive", "unarchived", next => { Assert.False(next.IsArchived); Assert.False(next.IsDeleted); }),
                     ("delete", "deleted", next => { Assert.True(next.IsDeleted); Assert.False(next.IsActive); })
                 })
        {
            repository.Setup(x => x.ReplaceAsync(
                    current,
                    It.IsAny<BannerDto>(),
                    expected,
                    "admin",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((BannerDto _, BannerDto next, string _, string _, CancellationToken _) =>
                {
                    assert(next);
                    return new BannerStoreResult(200, "ok", "", next);
                });

            BannerStoreResult result =
                await service.ChangeAsync(
                    id,
                    new BannerActionRequest(4, action, null),
                    "admin",
                    Ct);

            Assert.Equal(200, result.Status);
            Assert.Equal(5, result.Banner!.Version);
        }

        Assert.Equal(
            "banner.actionInvalid",
            (await service.ChangeAsync(
                id,
                new BannerActionRequest(4, "unknown", null),
                "admin",
                Ct)).Code);
    }

    [Fact]
    public async Task ServiceVisibleFiltersScopeScheduleSortAndFindsNextChange()
    {
        DateTimeOffset now = UtcNow();
        var repository = new Mock<IBannerRepository>();
        var rules = new BannerRules();

        BannerDto first =
            Banner("first", "above-content", 2, true) with
            {
                Pages = ["/Holy/Overview"],
                StartAtUtc = now.AddMinutes(-1),
                EndAtUtc = now.AddMinutes(5)
            };

        BannerDto second =
            Banner("second", "above-content", 1, true) with
            {
                Pages = [],
                StartAtUtc = null,
                EndAtUtc = now.AddMinutes(2)
            };

        BannerDto future =
            Banner("future", "above-content", 0, true) with
            {
                Pages = ["/holy/overview"],
                StartAtUtc = now.AddMinutes(1),
                EndAtUtc = now.AddMinutes(10)
            };

        BannerDto otherScope =
            Banner("other", "above-content", 0, true) with
            {
                Pages = ["/protection/overview"]
            };

        BannerDto archived =
            Banner("archived", "above-content", 0, true) with
            {
                IsArchived = true
            };

        repository.Setup(x => x.ReadAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                first,
                second,
                future,
                otherScope,
                archived
            ]);

        var service = new BannerStoreService(
            repository.Object,
            rules,
            new FixedTimeProvider(now));

        var result =
            await service.VisibleAsync(
                " holy/overview/ ",
                Ct);

        Assert.Equal(
            new[] { "second", "first" },
            result.Items.Select(x => x.InternalName));

        Assert.Equal(
            now.AddMinutes(1),
            result.NextChangeAtUtc);
    }

    [Fact]
    public void CompatibilityFacadeDelegatesPureRules()
    {
        BannerRequest request = Request();
        DateTimeOffset now = UtcNow();

        Assert.Equal("/holy", BannerStore.NormalizePath("holy/"));
        Assert.Null(BannerStore.Validate(request, true));

        BannerDto banner = Banner("visible", "above-content", 0, true);

        Assert.True(
            BannerStore.IsVisible(
                banner,
                "/",
                now));
    }

    private static BannerRequest Request() =>
        new(
            "notice",
            "Title",
            "Body",
            null,
            null,
            null,
            null,
            "information",
            "above-content",
            [],
            null,
            null,
            0,
            true,
            true,
            1);

    private static BannerDto Banner(
        string name,
        string position,
        int sortOrder,
        bool active)
    {
        DateTimeOffset now = UtcNow();

        return new BannerDto(
            Guid.NewGuid(),
            name,
            name + " title",
            name + " text",
            null,
            "",
            null,
            null,
            "information",
            position,
            [],
            null,
            null,
            sortOrder,
            true,
            active,
            false,
            false,
            1,
            now,
            now);
    }

    private static DateTimeOffset UtcNow() =>
        new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(
        DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            now;
    }
}
