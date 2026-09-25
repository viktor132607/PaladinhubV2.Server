using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class PageManagementRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryServiceListsAndGetsVisiblePages()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        ContentPage beta = Page(
            "holy",
            "beta",
            "Beta");
        ContentPage alpha = Page(
            "holy",
            "alpha",
            "Alpha");
        ContentPage ret = Page(
            "retribution",
            "ret",
            "Ret");

        db.ContentPages.AddRange(beta, alpha, ret);
        await db.SaveChangesAsync(Ct);

        var service =
            new PageManagementQueryService(db);

        List<ContentPage> pages =
            await service.ListAsync(Ct);

        Assert.Equal(
            ["Alpha", "Beta", "Ret"],
            pages.Select(page => page.Title));

        ContentPage? found =
            await service.GetAsync(alpha.Id, Ct);

        Assert.Equal(alpha.Id, found!.Id);
        Assert.Null(
            await service.GetAsync(99999, Ct));
    }

    [Fact]
    public void RequestPolicyCoversValidationNormalizationAndSlugRules()
    {
        var policy =
            new PageManagementRequestPolicy();

        Assert.Equal(
            "Request body is required.",
            policy.ValidateRequest(null));

        Assert.Equal(
            "Section must be Holy, Protection, or Retribution.",
            policy.ValidateRequest(
                Request(section: "shadow")));

        Assert.Equal(
            "Page title is required.",
            policy.ValidateRequest(
                Request(title: " ")));

        Assert.Equal(
            "Page title cannot exceed 200 characters.",
            policy.ValidateRequest(
                Request(
                    title: new string('x', 201))));

        Assert.Equal(
            "Slug is required.",
            policy.ValidateRequest(
                Request(slug: " ")));

        Assert.Equal(
            "Slug must contain letters or numbers.",
            policy.ValidateRequest(
                Request(slug: "--- !!! ---")));

        Assert.Equal(
            "Slug cannot exceed 100 characters.",
            policy.ValidateRequest(
                Request(
                    slug: new string('a', 101))));

        Assert.Null(
            policy.ValidateRequest(
                Request(
                    section: " Prot ",
                    slug: " My Fancy--Page ")));

        Assert.Equal(
            "holy",
            policy.NormalizeSection("Holy"));
        Assert.Equal(
            "protection",
            policy.NormalizeSection("prot"));
        Assert.Equal(
            "retribution",
            policy.NormalizeSection("RETRI"));
        Assert.Equal(
            "retribution",
            policy.NormalizeSection("ret"));
        Assert.Equal(
            string.Empty,
            policy.NormalizeSection("unknown"));

        Assert.Equal(
            "my-fancy-page",
            policy.Slugify(" -- My Fancy--Page!!! "));
        Assert.Equal(
            "abc123",
            policy.Slugify("ABC123"));
        Assert.Equal(
            string.Empty,
            policy.Slugify("---"));

        Assert.True(
            policy.IsReserved(
                "holy",
                "overview"));
        Assert.True(
            policy.IsReserved(
                "PROTECTION",
                "GEAR"));
        Assert.False(
            policy.IsReserved(
                "holy",
                "custom"));
    }

    [Fact]
    public async Task MutationServiceCreateCoversReservedConflictAndSuccess()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var policy =
            new PageManagementRequestPolicy();

        var service =
            new PageManagementMutationService(
                db,
                policy);

        PageManagementResult reserved =
            await service.CreateAsync(
                Request(
                    section: "Holy",
                    slug: "overview"),
                "admin",
                Ct);

        Assert.Equal(
            PageManagementError.ReservedRoute,
            reserved.Error);

        db.ContentPages.Add(
            Page(
                "holy",
                "custom",
                "Deleted",
                deleted: true));

        await db.SaveChangesAsync(Ct);

        PageManagementResult conflict =
            await service.CreateAsync(
                Request(
                    section: "Holy",
                    slug: "custom"),
                "admin",
                Ct);

        Assert.Equal(
            PageManagementError.SlugConflict,
            conflict.Error);

        PageManagementResult created =
            await service.CreateAsync(
                Request(
                    section: " Ret ",
                    title: "  New page  ",
                    slug: " New Route ",
                    published: false),
                "creator",
                Ct);

        Assert.Equal(
            PageManagementError.None,
            created.Error);
        Assert.NotNull(created.Page);
        Assert.Equal(
            "retribution",
            created.Page!.Section);
        Assert.Equal(
            "New page",
            created.Page.Title);
        Assert.Equal(
            "new-route",
            created.Page.Slug);
        Assert.False(created.Page.IsPublished);
        Assert.Equal(
            "creator",
            created.Page.UpdatedBy);
        Assert.Equal(
            "[]",
            created.Page.JsonLayout);

        PageRevision revision =
            await db.PageRevisions
                .SingleAsync(
                    item =>
                        item.PageId ==
                        created.Page.Id,
                    Ct);

        Assert.Equal(
            "created",
            revision.Action);
        Assert.Equal(
            "creator",
            revision.Actor);
    }

    [Fact]
    public async Task MutationServiceUpdateCoversAllExpectedResults()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var service =
            new PageManagementMutationService(
                db,
                new PageManagementRequestPolicy());

        Assert.Equal(
            PageManagementError.NotFound,
            (await service.UpdateAsync(
                99999,
                Request(),
                "admin",
                Ct)).Error);

        ContentPage page =
            Page(
                "holy",
                "custom",
                "Custom");

        ContentPage other =
            Page(
                "holy",
                "taken",
                "Taken");

        db.ContentPages.AddRange(page, other);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(
            PageManagementError.ReservedRoute,
            (await service.UpdateAsync(
                page.Id,
                Request(
                    slug: "gear",
                    rowVersion: page.RowVersion),
                "admin",
                Ct)).Error);

        Assert.Equal(
            PageManagementError.SlugConflict,
            (await service.UpdateAsync(
                page.Id,
                Request(
                    slug: "taken",
                    rowVersion: page.RowVersion),
                "admin",
                Ct)).Error);

        var malformed = Request(
            rowVersion: page.RowVersion);

        malformed = new SavePageRequest
        {
            Section = malformed.Section,
            Title = malformed.Title,
            Slug = malformed.Slug,
            IsPublished = malformed.IsPublished,
            RowVersionBase64 = "%%%"
        };

        Assert.Equal(
            PageManagementError.ConcurrencyConflict,
            (await service.UpdateAsync(
                page.Id,
                malformed,
                "admin",
                Ct)).Error);

        Assert.Equal(
            PageManagementError.ConcurrencyConflict,
            (await service.UpdateAsync(
                page.Id,
                Request(rowVersion: []),
                "admin",
                Ct)).Error);

        Assert.Equal(
            PageManagementError.ConcurrencyConflict,
            (await service.UpdateAsync(
                page.Id,
                Request(rowVersion: [9, 9]),
                "admin",
                Ct)).Error);

        byte[] version = page.RowVersion.ToArray();

        PageManagementResult updated =
            await service.UpdateAsync(
                page.Id,
                Request(
                    section: " Prot ",
                    title: " Updated ",
                    slug: " New Route ",
                    published: false,
                    rowVersion: version),
                "editor",
                Ct);

        Assert.Equal(
            PageManagementError.None,
            updated.Error);
        Assert.Equal(
            "protection",
            page.Section);
        Assert.Equal(
            "Updated",
            page.Title);
        Assert.Equal(
            "new-route",
            page.Slug);
        Assert.False(page.IsPublished);
        Assert.Equal(
            "editor",
            page.UpdatedBy);

        Assert.Contains(
            db.PageRevisions,
            revision =>
                revision.PageId == page.Id &&
                revision.Action == "updated" &&
                revision.Actor == "editor");
    }

    [Fact]
    public async Task MutationServiceMapsDatabaseConcurrencyException()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync(Ct);

        connection.CreateFunction<long, long>(
            "pg_advisory_xact_lock",
            value => value);

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new ThrowingPageDbContext(options);

        db.Database.EnsureCreated();

        ContentPage page =
            Page(
                "holy",
                "custom",
                "Custom");

        db.ContentPages.Add(page);
        await db.SaveChangesAsync(Ct);

        byte[] version = page.RowVersion.ToArray();

        db.ThrowConcurrency = true;

        var service =
            new PageManagementMutationService(
                db,
                new PageManagementRequestPolicy());

        PageManagementResult result =
            await service.UpdateAsync(
                page.Id,
                Request(rowVersion: version),
                "editor",
                Ct);

        Assert.Equal(
            PageManagementError.ConcurrencyConflict,
            result.Error);
    }

    [Fact]
    public async Task MutationServiceDeleteRecordsActualActor()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var service =
            new PageManagementMutationService(
                db,
                new PageManagementRequestPolicy());

        Assert.False(
            await service.DeleteAsync(
                99999,
                "deleter",
                Ct));

        ContentPage page =
            Page(
                "holy",
                "custom",
                "Custom");

        page.UpdatedBy = "previous-editor";

        db.ContentPages.Add(page);
        await db.SaveChangesAsync(Ct);

        Assert.True(
            await service.DeleteAsync(
                page.Id,
                "deleter",
                Ct));

        ContentPage deleted =
            await db.ContentPages
                .IgnoreQueryFilters()
                .SingleAsync(
                    item => item.Id == page.Id,
                    Ct);

        Assert.True(deleted.IsDeleted);
        Assert.True(deleted.IsArchived);
        Assert.False(deleted.IsPublished);
        Assert.Equal(
            "deleter",
            deleted.UpdatedBy);

        PageRevision revision =
            await db.PageRevisions
                .OrderByDescending(item => item.Version)
                .FirstAsync(
                    item => item.PageId == page.Id,
                    Ct);

        Assert.Equal(
            "deleted",
            revision.Action);
        Assert.Equal(
            "deleter",
            revision.Actor);
    }

    [Fact]
    public async Task FacadeDelegatesEveryOperation()
    {
        var queries =
            new Mock<IPageManagementQueryService>();

        var policy =
            new Mock<IPageManagementRequestPolicy>();

        var mutations =
            new Mock<IPageManagementMutationService>();

        ContentPage page =
            Page(
                "holy",
                "custom",
                "Custom");

        SavePageRequest request =
            Request();

        queries.Setup(x => x.ListAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([page]);

        queries.Setup(x => x.GetAsync(
                page.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        policy.Setup(x => x.ValidateRequest(request))
            .Returns("validation");

        mutations.Setup(x => x.CreateAsync(
                request,
                "admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PageManagementResult(
                    PageManagementError.None,
                    page));

        mutations.Setup(x => x.UpdateAsync(
                page.Id,
                request,
                "editor",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PageManagementResult(
                    PageManagementError.None,
                    page));

        mutations.Setup(x => x.DeleteAsync(
                page.Id,
                "deleter",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mutations.Setup(x => x.DeleteAsync(
                page.Id,
                "admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service =
            new PageManagementService(
                queries.Object,
                policy.Object,
                mutations.Object);

        Assert.Single(
            await service.ListAsync(Ct));
        Assert.Same(
            page,
            await service.GetAsync(
                page.Id,
                Ct));
        Assert.Equal(
            "validation",
            service.ValidateRequest(request));
        Assert.Equal(
            PageManagementError.None,
            (await service.CreateAsync(
                request,
                "admin",
                Ct)).Error);
        Assert.Equal(
            PageManagementError.None,
            (await service.UpdateAsync(
                page.Id,
                request,
                "editor",
                Ct)).Error);
        Assert.True(
            await service.DeleteAsync(
                page.Id,
                "deleter",
                Ct));
        Assert.True(
            await service.DeleteAsync(
                page.Id,
                Ct));

        Assert.Equal(
            "Holy",
            PageManagementService.Capitalize("holy"));
        Assert.Equal(
            string.Empty,
            PageManagementService.Capitalize(string.Empty));

        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        Assert.NotNull(
            new PageManagementService(db));
    }

    private static SavePageRequest Request(
        string? section = "Holy",
        string? title = "Custom page",
        string? slug = "custom",
        bool published = true,
        byte[]? rowVersion = null)
    {
        return new SavePageRequest
        {
            Section = section,
            Title = title,
            Slug = slug,
            IsPublished = published,
            RowVersionBase64 =
                rowVersion is null
                    ? null
                    : Convert.ToBase64String(rowVersion)
        };
    }

    private static ContentPage Page(
        string section,
        string slug,
        string title,
        bool deleted = false)
    {
        return new ContentPage
        {
            Section = section,
            Slug = slug,
            Title = title,
            JsonLayout = "[]",
            IsPublished = true,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            RowVersion = Array.Empty<byte>()
        };
    }

    private sealed class ThrowingPageDbContext :
        AppDbContext
    {
        public ThrowingPageDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public bool ThrowConcurrency { get; set; }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency)
            {
                throw new DbUpdateConcurrencyException();
            }

            return base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
        }
    }
}
