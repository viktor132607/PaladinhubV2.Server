using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Common.Models.Navigation;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class NavigationControllerTests
{
    [Fact]
    public async Task List_ReturnsSortedRowsWithChildCount()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent", sortOrder: 1);
        await AddAsync(db, "Child", "/child", parentId: parent.Id, sortOrder: 0);
        await AddAsync(db, "Zulu", "/z", sortOrder: 2);
        await AddAsync(db, "Alpha", "/a", sortOrder: 1);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).List(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<NavigationAdminRow>>(result.Value);

        Assert.Equal(new[] { "Child", "Alpha", "Parent", "Zulu" }, rows.Select(row => row.Name));
        Assert.Equal(1, rows.Single(row => row.Id == parent.Id).ChildCount);
    }

    [Fact]
    public async Task Public_ReturnsOnlyActiveLinksWithActiveParents()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink active = await AddAsync(db, "Active", "/active");
        NavigationLink archivedParent = await AddAsync(db, "Archived parent", "/archived", isArchived: true);
        await AddAsync(db, "Hidden child", "/hidden", parentId: archivedParent.Id);
        await AddAsync(db, "Archived", "/a", isArchived: true);
        await AddAsync(db, "Deleted", "/d", isDeleted: true);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).Public(TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<PublicNavigationRow>>(result.Value);

        PublicNavigationRow row = Assert.Single(rows);
        Assert.Equal(active.Id, row.Id);
    }

    [Fact]
    public async Task History_ReturnsNewestRevisionFirst()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Link", "/link");
        db.NavigationRevisions.AddRange(
            new NavigationRevision { NavigationId = link.Id, Version = 1, Action = "created" },
            new NavigationRevision { NavigationId = link.Id, Version = 3, Action = "updated" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).History(link.Id, TestContext.Current.CancellationToken));
        var rows = Assert.IsType<List<NavigationRevision>>(result.Value);
        Assert.Equal(new[] { 3, 1 }, rows.Select(row => row.Version));
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("   ", "/valid"), TestContext.Current.CancellationToken));
        Assert.Equal("Name is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/bad\\path")]
    public async Task Create_UnsafeHref_ReturnsBadRequest(string href)
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Link", href), TestContext.Current.CancellationToken));
        Assert.Equal("Use a site path beginning with / or an http/https URL.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidLocation_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Link", "/link", location: "footer"), TestContext.Current.CancellationToken));
        Assert.Equal("Choose primary or utility navigation.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateSiblingName_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        await AddAsync(db, "Docs", "/docs");

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request(" docs ", "/other"), TestContext.Current.CancellationToken));

        Assert.Equal("A navigation link with this name already exists under this menu.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ChildWithDifferentLocation_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent", location: "primary");

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Child", "/child", location: "utility", parentId: parent.Id),
            TestContext.Current.CancellationToken));

        Assert.Equal("Use the same menu location as the parent.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ChildOfArchivedParent_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent", isArchived: true);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            Request("Child", "/child", parentId: parent.Id), TestContext.Current.CancellationToken));

        Assert.Equal("An active child link cannot belong to an archived parent link.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidLink_TrimsValuesAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db, "nav-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Create(
            new NavigationRequest("  Guides  ", "  Main guides  ", "  /guides  ", "primary", true, null, 7, false, 0),
            TestContext.Current.CancellationToken));
        var link = Assert.IsType<NavigationLink>(result.Value);

        Assert.Equal("Guides", link.Name);
        Assert.Equal("Main guides", link.Description);
        Assert.Equal("/guides", link.Href);
        NavigationRevision revision = Assert.Single(db.NavigationRevisions);
        Assert.Equal("created", revision.Action);
        Assert.Equal("nav-admin", revision.Actor);
    }

    [Fact]
    public async Task Edit_MissingLink_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        IActionResult result = await CreateController(db).Edit(
            999, Request("Missing", "/missing", version: 1), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_StaleVersion_ReturnsConflictMessage()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Link", "/link", version: 4);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Edit(
            link.Id, Request("Link", "/link", version: 3), TestContext.Current.CancellationToken));

        Assert.Equal("This navigation link changed in another session. Refresh before saving.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_LinkWithChildrenCannotChangeLocation()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent", location: "primary");
        await AddAsync(db, "Child", "/child", location: "primary", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            parent.Id,
            Request("Parent", "/parent", location: "utility", version: parent.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Move child links to another parent before changing menu location.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_CannotArchiveParentWithActiveChild()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent");
        await AddAsync(db, "Child", "/child", parentId: parent.Id);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            parent.Id,
            new NavigationRequest(parent.Name, null, parent.Href, parent.Location, false, null, 0, true, parent.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Archive or move active child links first.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_LinkWithChild_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink parent = await AddAsync(db, "Parent", "/parent");
        await AddAsync(db, "Child", "/child", parentId: parent.Id);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Delete(
            parent.Id, parent.Version, TestContext.Current.CancellationToken));

        Assert.Equal("Move child links before deleting this entry, or archive it.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_UnusedLink_ReturnsNoContentAndRecordsActor()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Link", "/link");

        IActionResult result = await CreateController(db, "deleter").Delete(
            link.Id, link.Version, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.True(link.IsDeleted);
        NavigationRevision revision = Assert.Single(db.NavigationRevisions);
        Assert.Equal("deleted", revision.Action);
        Assert.Equal("deleter", revision.Actor);
    }

    [Fact]
    public async Task Restore_MissingRevision_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Deleted", "/deleted", version: 2, isDeleted: true);

        IActionResult result = await CreateController(db).Restore(
            link.Id,
            new RestoreNavigationRequest(Guid.NewGuid(), link.Version),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRevision_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Deleted", "/deleted", version: 2, isDeleted: true);
        var revision = new NavigationRevision
        {
            NavigationId = link.Id,
            Version = 2,
            Action = "deleted",
            Snapshot = JsonSerializer.Serialize(new NavigationLink { Id = link.Id, Name = "Deleted", Href = "/deleted", IsDeleted = true, Version = 2 })
        };
        db.NavigationRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Restore(
            link.Id,
            new RestoreNavigationRequest(revision.Id, link.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("Select a revision before deletion.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_DuplicateHistoricalName_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        await AddAsync(db, "Taken", "/taken");
        NavigationLink link = await AddAsync(db, "Deleted", "/deleted", version: 2, isDeleted: true);
        var revision = new NavigationRevision
        {
            NavigationId = link.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(new NavigationLink { Id = link.Id, Name = "Taken", Href = "/old", Location = "primary", Version = 1 })
        };
        db.NavigationRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<ConflictObjectResult>(await CreateController(db).Restore(
            link.Id,
            new RestoreNavigationRequest(revision.Id, link.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal("A navigation link with this name already exists under this menu.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Restore_ValidRevision_ReturnsRestoredLink()
    {
        await using AppDbContext db = CreateContext();
        NavigationLink link = await AddAsync(db, "Deleted", "/deleted", version: 2, isDeleted: true);
        var revision = new NavigationRevision
        {
            NavigationId = link.Id,
            Version = 1,
            Action = "created",
            Snapshot = JsonSerializer.Serialize(new NavigationLink { Id = link.Id, Name = "Recovered", Description = "Old", Href = "/recovered", Location = "primary", Version = 1 })
        };
        db.NavigationRevisions.Add(revision);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(await CreateController(db, "restorer").Restore(
            link.Id,
            new RestoreNavigationRequest(revision.Id, link.Version),
            TestContext.Current.CancellationToken));
        var restored = Assert.IsType<NavigationLink>(result.Value);

        Assert.False(restored.IsDeleted);
        Assert.Equal("Recovered", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Contains(db.NavigationRevisions, value => value.Action == "restored" && value.Actor == "restorer");
    }

    private static NavigationRequest Request(
        string name,
        string href,
        string location = "primary",
        int? parentId = null,
        int version = 0) =>
        new(name, null, href, location, false, parentId, 0, false, version);

    private static NavigationController CreateController(AppDbContext db, string actor = "actor-1")
    {
        var controller = new NavigationController(db);
        ControllerTestSupport.Attach(controller, ControllerTestSupport.CreateHttpContext(actor));
        return controller;
    }

    private static async Task<NavigationLink> AddAsync(
        AppDbContext db,
        string name,
        string href,
        string location = "primary",
        int? parentId = null,
        int sortOrder = 0,
        int version = 1,
        bool isArchived = false,
        bool isDeleted = false)
    {
        var link = new NavigationLink
        {
            Name = name,
            Description = string.Empty,
            Href = href,
            Location = location,
            ParentId = parentId,
            SortOrder = sortOrder,
            Version = version,
            IsArchived = isArchived || isDeleted,
            IsDeleted = isDeleted
        };
        db.NavigationLinks.Add(link);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return link;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"navigation-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
