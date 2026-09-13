using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Seo;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoTests
{
    private static SeoRequest Request(string path = "/Home/Home") =>
        new(null, path, "Title", "Description", "", "", "", "", null, null, 1);

    [Theory]
    [InlineData("/Admin/Seo")]
    [InlineData("/api/seo")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/Home/../Admin")]
    [InlineData("/Home?preview=true")]
    public void RejectsPrivateOrUnsafePaths(string path) =>
        Assert.NotNull(SeoService.Validate(Request(path)));

    [Fact]
    public async Task InvalidCreateDoesNotWrite()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new SeoController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = Assert.IsType<ObjectResult>(await controller.Create(
            Request() with { CanonicalUrl = "javascript:alert(1)" }, TestContext.Current.CancellationToken));
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(db.SeoEntries);
        Assert.Empty(db.SeoRevisions);
    }

    [Fact]
    public async Task NormalizesBeforeCheckingDuplicatesAndPreservesExistingEntry()
    {
        await using var db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(200, (await service.Save(null, Request(), "test", ct)).Status);
        Assert.Equal(409, (await service.Save(null, Request("/home/home/"), "test", ct)).Status);
        Assert.Single(db.SeoEntries);
        Assert.Single(db.SeoRevisions);
    }

    [Fact]
    public async Task StaleUpdateDoesNotChangeContentOrAddRevision()
    {
        await using var db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        var ct = TestContext.Current.CancellationToken;
        var created = (await service.Save(null, Request(), "test", ct)).Entry!;
        var result = await service.Save(created.Id, Request() with { Title = "Wrong", Version = 0 }, "test", ct);
        Assert.Equal(409, result.Status);
        Assert.Equal("Title", (await db.SeoEntries.SingleAsync(ct)).Title);
        Assert.Single(db.SeoRevisions);
    }

    [Fact]
    public async Task CannotUnarchiveWhenReferencedMediaIsMissing()
    {
        await using var db = PageBuilderSqliteTestDatabase.CreateContext();
        var id = Guid.NewGuid();
        var entry = new SeoEntry { Path = "/Home/Home", IsArchived = true,
            ImageUrl = $"https://example.test/api/spell-icons/{id}" };
        // Missing media is rejected by the same check as archived media.
        db.SeoEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result = await new SeoService(db).Change(entry.Id, 1, "unarchive", null, "test", TestContext.Current.CancellationToken);
        Assert.Equal(409, result.Status);
        Assert.True(entry.IsArchived);
        Assert.Equal(1, entry.Version);
        Assert.Empty(db.SeoRevisions);
    }

    [Fact]
    public async Task PublicSnapshotOmitsArchivedAndDeletedEntriesAndInternalHistory()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SeoEntries.AddRange(
            new SeoEntry { Path = "*", Title = "Visible" },
            new SeoEntry { Path = "/archived", Title = "Archived", IsArchived = true },
            new SeoEntry { Path = "/deleted", Title = "Deleted", IsDeleted = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var result = Assert.IsType<OkObjectResult>(await new PublicSeoController(db, configuration)
            .Snapshot(TestContext.Current.CancellationToken));
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        var entries = payload.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("Visible", entries[0].GetProperty("Title").GetString());
        Assert.False(entries[0].TryGetProperty("IsDeleted", out _));
        Assert.False(entries[0].TryGetProperty("Actor", out _));
    }

    [Fact]
    public async Task DeleteRequiresRevisionRecoveryAndRecordsTheActor()
    {
        await using var db = PageBuilderSqliteTestDatabase.CreateContext();
        var service = new SeoService(db);
        var ct = TestContext.Current.CancellationToken;
        var created = (await service.Save(null, Request(), "creator", ct)).Entry!;
        var original = await db.SeoRevisions.SingleAsync(ct);
        Assert.Equal(200, (await service.Change(created.Id, 1, "delete", null, "deleter", ct)).Status);
        Assert.Equal(409, (await service.Change(created.Id, 2, "unarchive", null, "test", ct)).Status);
        var deleted = await db.SeoRevisions.SingleAsync(x => x.Version == 2, ct);
        Assert.Equal("deleter", deleted.Actor);
        Assert.Equal(400, (await service.Change(created.Id, 2, "restore", deleted.Id, "test", ct)).Status);
        Assert.Equal(200, (await service.Change(created.Id, 2, "restore", original.Id, "restorer", ct)).Status);
        Assert.False(created.IsDeleted);
        Assert.False(created.IsArchived);
        Assert.Equal(3, created.Version);
        Assert.Equal(3, await db.SeoRevisions.CountAsync(ct));
    }
}
