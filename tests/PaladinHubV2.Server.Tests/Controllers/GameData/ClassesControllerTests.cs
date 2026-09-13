using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class ClassesControllerTests
{
    [Fact]
    public async Task List_ReturnsSortedRowsWithSpecializationCounts()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline paladin = await AddAsync(db, "Paladin", sortOrder: 1);
        await AddAsync(db, "Holy", parentId: paladin.Id, sortOrder: 0);
        await AddAsync(db, "Warrior", sortOrder: 2);
        await AddAsync(db, "Druid", sortOrder: 1);
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<DisciplineListItem>>(result.Value);

        Assert.Equal(new[] { "Holy", "Druid", "Paladin", "Warrior" }, rows.Select(row => row.Name));
        Assert.Equal(1, rows.Single(row => row.Id == paladin.Id).ChildCount);
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Paladin");
        db.DisciplineRevisions.AddRange(
            new DisciplineRevision { DisciplineId = discipline.Id, Version = 1, Action = "created" },
            new DisciplineRevision { DisciplineId = discipline.Id, Version = 5, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).History(
            discipline.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<DisciplineRevision>>(result.Value);

        Assert.Equal(new[] { 5, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("   "), TestContext.Current.CancellationToken));
        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateSiblingName_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline parent = await AddAsync(db, "Paladin");
        await AddAsync(db, "Holy", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request(" holy ", parent.Id), TestContext.Current.CancellationToken));

        Assert.Equal("A class or specialization with this name already exists under this class.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_SpecializationWithMissingClass_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Holy", 999), TestContext.Current.CancellationToken));
        Assert.Equal("A specialization must belong to a top-level class.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_SpecializationUnderSpecialization_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline parent = await AddAsync(db, "Paladin");
        GameDiscipline specialization = await AddAsync(db, "Holy", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Nested", specialization.Id), TestContext.Current.CancellationToken));

        Assert.Equal("A specialization must belong to a top-level class.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ActiveSpecializationUnderArchivedClass_ReturnsValidationMessage()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline parent = await AddAsync(db, "Archived", isArchived: true);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Holy", parent.Id), TestContext.Current.CancellationToken));

        Assert.Equal("An active specialization cannot belong to an archived class.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsValuesAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "class-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new DisciplineRequest("  Paladin  ", "  Light wielder  ", null, 2, false, 0),
            TestContext.Current.CancellationToken));
        var discipline = Assert.IsType<GameDiscipline>(result.Value);

        Assert.Equal("Paladin", discipline.Name);
        Assert.Equal("Light wielder", discipline.Description);
        DisciplineRevision revision = Assert.Single(db.DisciplineRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("class-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingEntry_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        IActionResult result = await CreateController(db).Edit(
            999, Request("Missing", version: 1), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Paladin", version: 3);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Edit(
            discipline.Id, Request("Paladin", version: 2), TestContext.Current.CancellationToken));

        Assert.Equal("This class or specialization changed in another session. Refresh before saving.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ClassWithChildrenCannotBecomeSpecialization()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline owner = await AddAsync(db, "Owner");
        GameDiscipline moving = await AddAsync(db, "Moving");
        await AddAsync(db, "Child", parentId: moving.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            moving.Id, Request("Moving", owner.Id, moving.Version), TestContext.Current.CancellationToken));

        Assert.Equal("A class with specializations cannot become a specialization.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_CannotAssignClassToItself()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Paladin");

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            discipline.Id, Request("Paladin", discipline.Id, discipline.Version), TestContext.Current.CancellationToken));

        Assert.Equal("A class cannot belong to itself or its specializations.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_CannotArchiveClassWithActiveSpecialization()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline parent = await AddAsync(db, "Paladin");
        await AddAsync(db, "Holy", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            parent.Id,
            new DisciplineRequest(parent.Name, null, null, 0, true, parent.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Archive or move active specializations first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_ValidRequest_ReturnsUpdatedEntryAndArchiveRevision()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Old");
        var controller = CreateController(db, "editor");

        var result = Assert.IsType<OkObjectResult>(await controller.Edit(
            discipline.Id,
            new DisciplineRequest("  New  ", "  Changed  ", null, 4, true, discipline.Version),
            TestContext.Current.CancellationToken));
        var updated = Assert.IsType<GameDiscipline>(result.Value);

        Assert.Equal("New", updated.Name);
        Assert.True(updated.IsArchived);
        Assert.Equal(2, updated.Version);
        DisciplineRevision revision = Assert.Single(db.DisciplineRevisions);
        Assert.Equal("archived", revision.Action);
        Assert.Equal("editor", revision.Actor);
    }

    [Fact]
    public async Task Delete_ClassWithSpecialization_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline parent = await AddAsync(db, "Paladin");
        await AddAsync(db, "Holy", parentId: parent.Id);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Delete(
            parent.Id, parent.Version, TestContext.Current.CancellationToken));

        Assert.Contains("Move specializations", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_UnusedEntry_ReturnsNoContentAndMarksDeleted()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Unused");

        IActionResult result = await CreateController(db, "deleter").Delete(
            discipline.Id, discipline.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(discipline.IsDeleted);
        Assert.Equal(2, discipline.Version);
        DisciplineRevision revision = Assert.Single(db.DisciplineRevisions);
        Assert.Equal("deleted", revision.Action);
        Assert.Equal("deleter", revision.Actor);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        IActionResult result = await CreateController(db).Restore(
            discipline.Id, new RevisionRestoreRequest(Guid.NewGuid(), discipline.Version), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        var revision = new DisciplineRevision
        {
            DisciplineId = discipline.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(new GameDiscipline { Id = discipline.Id, Name = "Deleted", IsDeleted = true, Version = 2 })
        };
        db.DisciplineRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Restore(
            discipline.Id, new RevisionRestoreRequest(revision.Id, discipline.Version), TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_ValidRevision_ReturnsRestoredEntry()
    {
        await using AppDbContext db = CreateContext();
        GameDiscipline discipline = await AddAsync(db, "Deleted", version: 2, isDeleted: true);
        var revision = new DisciplineRevision
        {
            DisciplineId = discipline.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(new GameDiscipline { Id = discipline.Id, Name = "Recovered", Description = "Historical", Version = 1 })
        };
        db.DisciplineRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db, "restorer").Restore(
            discipline.Id, new RevisionRestoreRequest(revision.Id, discipline.Version), TestContext.Current.CancellationToken));
        var restored = Assert.IsType<GameDiscipline>(result.Value);

        Assert.False(restored.IsDeleted);
        Assert.Equal("Recovered", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.DisciplineRevisions, value => value.Action == "restored" && value.Actor == "restorer");
    }

    private static DisciplineRequest Request(string name, int? parentId = null, int version = 0) =>
        new(name, null, parentId, 0, false, version);

    private static ClassesController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new ClassesController(new DisciplineAdminService(db, new GameDataAssignmentService(db)));
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static async Task<GameDiscipline> AddAsync(
        AppDbContext db,
        string name,
        int? parentId = null,
        int sortOrder = 0,
        int version = 1,
        bool isArchived = false,
        bool isDeleted = false)
    {
        var discipline = new GameDiscipline
        {
            Name = name,
            Description = string.Empty,
            ParentId = parentId,
            SortOrder = sortOrder,
            Version = version,
            IsArchived = isArchived || isDeleted,
            IsDeleted = isDeleted
        };
        db.GameDisciplines.Add(discipline);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return discipline;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"classes-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
