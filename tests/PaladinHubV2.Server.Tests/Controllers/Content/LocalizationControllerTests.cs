using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Localization;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content;

public sealed class LocalizationControllerTests
{
    [Fact]
    public async Task List_ReturnsLanguagesOrderedByCode()
    {
        await using AppDbContext db = CreateContext();
        db.SiteLanguages.AddRange(
            Language("fr", "French"),
            Language("bg", "Bulgarian"),
            Language("en", "English"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<SiteLanguage>>(result.Value);

        Assert.Equal(new[] { "bg", "en", "fr" }, rows.Select(row => row.Code));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        db.SiteLanguages.Add(language);
        db.LanguageRevisions.AddRange(
            new LanguageRevision { LanguageId = language.Id, Version = 1, Action = "created" },
            new LanguageRevision { LanguageId = language.Id, Version = 4, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(language.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<LanguageRevision>>(result.Value);

        Assert.Equal(new[] { 4, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_InvalidCode_Returns400WithValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            new LanguageRequest("bad code", "Bad", EmptyTranslations(), 0),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            "Enter a language code such as en, bg or pt-BR.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409()
    {
        await using AppDbContext db = CreateContext();
        db.SiteLanguages.Add(Language("bg", "Bulgarian"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            new LanguageRequest("bg", "Another Bulgarian", EmptyTranslations(), 0),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "This language code already exists. Restore the existing language if deleted.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidLanguage_Returns200AndRecordsNamedActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "localization-admin");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            new LanguageRequest(
                "bg",
                "  Bulgarian  ",
                new Dictionary<string, string> { ["nav.home"] = "Начало" },
                0),
            TestContext.Current.CancellationToken));
        var language = Assert.IsType<SiteLanguage>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("bg", language.Code);
        Assert.Equal("Bulgarian", language.Name);
        LanguageRevision revision = Assert.Single(db.LanguageRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("localization-admin", revision.Actor);
    }

    [Fact]
    public async Task Update_MissingLanguage_Returns404()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            Guid.NewGuid(),
            new LanguageRequest("bg", "Bulgarian", EmptyTranslations(), 1),
            TestContext.Current.CancellationToken));

        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian", version: 3);
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            language.Id,
            new LanguageRequest("bg", "Bulgarian", EmptyTranslations(), 2),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Language changed. Reload before saving.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ChangingCode_Returns400()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            language.Id,
            new LanguageRequest("en", "Bulgarian", EmptyTranslations(), language.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            "Language codes cannot be changed. Create a new language instead.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ArchivedLanguage_Returns409()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        language.IsArchived = true;
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            language.Id,
            new LanguageRequest("bg", "Bulgarian", EmptyTranslations(), language.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "Restore or unarchive this language before editing.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ValidLanguage_ReturnsUpdatedEntity()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "editor");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            language.Id,
            new LanguageRequest("bg", "  Bulgarian updated  ", new() { ["key"] = "value" }, language.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<SiteLanguage>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Bulgarian updated", updated.Name);
        Assert.Equal(2, updated.Version);
        LanguageRevision revision = Assert.Single(db.LanguageRevisions);
        Assert.Equal("updated", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Change_EnglishArchive_Returns409()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("en", "English");
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "archive", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Contains("fallback language", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_UnknownAction_Returns400()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "explode", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unknown action.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_Archive_Returns200AndUpdatesState()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian");
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "archiver");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "archive", null),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<SiteLanguage>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        Assert.Equal("archiver", Assert.Single(db.LanguageRevisions).Actor);
    }

    [Fact]
    public async Task Change_RestoreMissingRevision_Returns404()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian", version: 2);
        language.IsDeleted = true;
        language.IsArchived = true;
        db.SiteLanguages.Add(language);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "restore", Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Revision not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreDeletedRevision_Returns400()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Bulgarian", version: 2);
        language.IsDeleted = true;
        language.IsArchived = true;
        db.SiteLanguages.Add(language);
        var revision = new LanguageRevision
        {
            LanguageId = language.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(Language("bg", "Bulgarian", version: 2, isDeleted: true))
        };
        db.LanguageRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "restore", revision.Id),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Choose a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreValidRevision_ReturnsRestoredLanguage()
    {
        await using AppDbContext db = CreateContext();
        SiteLanguage language = Language("bg", "Deleted", version: 2);
        language.IsDeleted = true;
        language.IsArchived = true;
        db.SiteLanguages.Add(language);
        SiteLanguage snapshot = Language("bg", "Historical", version: 1);
        snapshot.ResourcesJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["key"] = "old" });
        var revision = new LanguageRevision
        {
            LanguageId = language.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.LanguageRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, "restorer");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            language.Id,
            new LocalizationController.ChangeRequest(language.Version, "restore", revision.Id),
            TestContext.Current.CancellationToken));
        var restored = Assert.IsType<SiteLanguage>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.False(restored.IsDeleted);
        Assert.False(restored.IsArchived);
        Assert.Equal("Historical", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.LanguageRevisions, value => value.Action == "restore" && value.Actor == "restorer");
    }

    private static LocalizationController CreateController(AppDbContext db, string actor = "admin")
    {
        var controller = new LocalizationController(db);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, actor) },
                "Test"))
        };
        ControllerTestSupport.Attach(controller, context);
        return controller;
    }

    private static Dictionary<string, string> EmptyTranslations() => new();

    private static SiteLanguage Language(
        string code,
        string name,
        int version = 1,
        bool isDeleted = false)
    {
        return new SiteLanguage
        {
            Code = code,
            Name = name,
            ResourcesJson = "{}",
            Version = version,
            IsDeleted = isDeleted,
            IsArchived = isDeleted
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"localization-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
