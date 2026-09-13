using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class ContentTemplatesControllerTests
{
    private const string ValidLayout = "[{\"type\":\"paragraph\"}]";

    [Fact]
    public async Task List_FiltersByKindAndOrdersByName()
    {
        await using AppDbContext db = CreateContext();
        db.ContentTemplates.AddRange(
            Template("Zulu", "block"),
            Template("Alpha", "block"),
            Template("Talent", "talent-tree"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List("block", TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<ContentTemplate>>(result.Value);

        Assert.Equal(new[] { "Alpha", "Zulu" }, rows.Select(row => row.Name));
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Template", "block");
        db.ContentTemplates.Add(template);
        db.ContentTemplateRevisions.AddRange(
            new ContentTemplateRevision { TemplateId = template.Id, Version = 1, Action = "created" },
            new ContentTemplateRevision { TemplateId = template.Id, Version = 4, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.History(template.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<ContentTemplateRevision>>(result.Value);

        Assert.Equal(new[] { 4, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_UnknownKind_Returns400()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "unknown",
            Request("Template"),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unknown template kind.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidatorErrors_ReturnJoinedMessage()
    {
        await using AppDbContext db = CreateContext();
        var validator = new Mock<IJsonLayoutValidator>();
        validator.Setup(value => value.ValidateOrThrow(It.IsAny<string>()))
            .Throws(new JsonLayoutValidationException(new[] { "First error.", "Second error." }));
        var controller = CreateController(db, validator);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "block",
            Request("Template"),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("First error. Second error.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidJson_Returns400()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "block",
            new TemplateRequest("Template", "", "{not-json", 0),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Invalid template JSON.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_EmptyLayout_Returns400()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "block",
            new TemplateRequest("Template", "", "[]", 0),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Choose between 1 and 100 blocks.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_TalentTemplateWithWrongBlock_Returns400()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "talent-tree",
            Request("Talent"),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            "A talent template must contain exactly one dynamic talent tree.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await using AppDbContext db = CreateContext();
        db.ContentTemplates.Add(Template("Existing", "block"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "block",
            Request(" existing "),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("A template with this name already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidTemplate_Returns200AndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, actor: "template-admin");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Create(
            "block",
            new TemplateRequest("  Guide card  ", "  Description  ", ValidLayout, 0),
            TestContext.Current.CancellationToken));
        var template = Assert.IsType<ContentTemplate>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Guide card", template.Name);
        Assert.Equal("Description", template.Description);
        ContentTemplateRevision revision = Assert.Single(db.ContentTemplateRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("template-admin", revision.Actor);
    }

    [Fact]
    public async Task Update_MissingTemplate_Returns404()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            Guid.NewGuid(), "block", Request("Template"), TestContext.Current.CancellationToken));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Template not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Template", "block", version: 3);
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            template.Id, "block", Request("Template", version: 2), TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Template changed. Reload the library first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ArchivedTemplate_Returns409()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Template", "block");
        template.IsArchived = true;
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            template.Id, "block", Request("Template", template.Version), TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "Restore or unarchive the template before editing.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_ValidTemplate_ReturnsUpdatedEntity()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Old", "block");
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, actor: "editor");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Update(
            template.Id,
            "block",
            new TemplateRequest("  New  ", "  Updated  ", ValidLayout, template.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<ContentTemplate>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("New", updated.Name);
        Assert.Equal(2, updated.Version);
        ContentTemplateRevision revision = Assert.Single(db.ContentTemplateRevisions);
        Assert.Equal("updated", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Change_UnknownAction_Returns400()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Template", "block");
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "explode", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unknown action.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_Archive_Returns200AndUpdatesVersion()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Template", "block");
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, actor: "archiver");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "archive", null),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<ContentTemplate>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        Assert.Equal("archiver", Assert.Single(db.ContentTemplateRevisions).Actor);
    }

    [Fact]
    public async Task Change_DeletedTemplateRequiresRestore()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Deleted", "block", version: 2, isDeleted: true);
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "archive", null),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Restore the deleted template first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreMissingRevision_Returns404()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Deleted", "block", version: 2, isDeleted: true);
        db.ContentTemplates.Add(template);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "restore", Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Revision not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreDeletedRevision_Returns400()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Deleted", "block", version: 2, isDeleted: true);
        db.ContentTemplates.Add(template);
        ContentTemplate deletedSnapshot = Template("Deleted", "block", version: 2, isDeleted: true);
        var revision = new ContentTemplateRevision
        {
            TemplateId = template.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(deletedSnapshot)
        };
        db.ContentTemplateRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "restore", revision.Id),
            TestContext.Current.CancellationToken));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreDuplicateHistoricalName_Returns409()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate existing = Template("Taken", "block");
        ContentTemplate template = Template("Deleted", "block", version: 2, isDeleted: true);
        db.ContentTemplates.AddRange(existing, template);
        ContentTemplate snapshot = Template("Taken", "block");
        snapshot.Id = template.Id;
        var revision = new ContentTemplateRevision
        {
            TemplateId = template.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.ContentTemplateRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "restore", revision.Id),
            TestContext.Current.CancellationToken));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "This template name is now in use. Rename the other template first.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Change_RestoreValidRevision_ReturnsRestoredTemplate()
    {
        await using AppDbContext db = CreateContext();
        ContentTemplate template = Template("Deleted", "block", version: 2, isDeleted: true);
        db.ContentTemplates.Add(template);
        ContentTemplate snapshot = Template("Historical", "block");
        snapshot.Id = template.Id;
        snapshot.Description = "Old description";
        var revision = new ContentTemplateRevision
        {
            TemplateId = template.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(snapshot)
        };
        db.ContentTemplateRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, actor: "restorer");

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Change(
            template.Id,
            new ContentTemplatesController.ChangeRequest(template.Version, "restore", revision.Id),
            TestContext.Current.CancellationToken));
        var restored = Assert.IsType<ContentTemplate>(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.False(restored.IsDeleted);
        Assert.Equal("Historical", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.ContentTemplateRevisions, value => value.Action == "restore" && value.Actor == "restorer");
    }

    private static ContentTemplatesController CreateController(
        AppDbContext db,
        Mock<IJsonLayoutValidator>? validator = null,
        string actor = "admin")
    {
        validator ??= new Mock<IJsonLayoutValidator>();
        var controller = new ContentTemplatesController(db, validator.Object);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, actor) },
                "Test"))
        };
        ControllerTestSupport.Attach(controller, context);
        return controller;
    }

    private static TemplateRequest Request(string name, int version = 0) =>
        new(name, string.Empty, ValidLayout, version);

    private static ContentTemplate Template(
        string name,
        string kind,
        int version = 1,
        bool isDeleted = false)
    {
        return new ContentTemplate
        {
            Name = name,
            Kind = kind,
            JsonLayout = ValidLayout,
            Version = version,
            IsDeleted = isDeleted,
            IsArchived = isDeleted
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"content-templates-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
