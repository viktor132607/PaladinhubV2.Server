using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class TalentPagesControllerTests
{
    [Fact]
    public async Task List_ReturnsOnlyDynamicTalentPages()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        db.ContentPages.AddRange(
            Page("holy", "dynamic", "Dynamic", "[{\"type\":\"talenttree.dynamic\"}]"),
            Page("holy", "normal", "Normal", "[]"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.List(TestContext.Current.CancellationToken));
        var rows = Assert.IsAssignableFrom<IEnumerable>(result.Value).Cast<object>().ToList();

        object row = Assert.Single(rows);
        Assert.Equal("Dynamic", ControllerTestSupport.ReadString(row, "title"));
        Assert.Equal("dynamic", ControllerTestSupport.ReadString(row, "slug"));
    }

    [Fact]
    public async Task Get_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        IActionResult result = await controller.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ExistingPage_ReturnsDetails()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        ContentPage page = Page("protection", "tank", "Tank", "[]");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.Get(page.Id));

        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal("Tank", ControllerTestSupport.ReadString(result.Value, "title"));
        Assert.Equal("protection", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("tank", ControllerTestSupport.ReadString(result.Value, "slug"));
        Assert.Equal("[]", ControllerTestSupport.ReadString(result.Value, "jsonLayout"));
        Assert.Equal(Convert.ToBase64String(page.RowVersion), ControllerTestSupport.ReadString(result.Value, "rowVersionBase64"));
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            Request(title: " ", section: "holy", slug: "build"),
            TestContext.Current.CancellationToken));

        Assert.Equal("Page title is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidSection_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            Request(title: "Build", section: "mage", slug: "build"),
            TestContext.Current.CancellationToken));

        Assert.Equal("Invalid section.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ReservedSlug_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(
            Request(title: "Build", section: "holy", slug: "talents"),
            TestContext.Current.CancellationToken));

        Assert.Equal(
            "Choose a unique slug, different from the existing guide pages.",
            ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        db.ContentPages.Add(Page("holy", "build", "Existing", "[]"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<ConflictObjectResult>(await controller.Create(
            Request(title: "Build", section: "holy", slug: "build"),
            TestContext.Current.CancellationToken));

        Assert.Equal("Slug already exists.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAndPersistsPage()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db, "talent-admin");

        var result = Assert.IsType<CreatedResult>(await controller.Create(
            Request(title: "  Mythic Build  ", section: "Holy", slug: "My Build"),
            TestContext.Current.CancellationToken));

        ContentPage page = Assert.Single(db.ContentPages);
        Assert.Equal("Mythic Build", page.Title);
        Assert.Equal("holy", page.Section);
        Assert.Equal("my-build", page.Slug);
        Assert.Equal("talent-admin", page.UpdatedBy);
        Assert.Equal($"/Admin/api/talent-pages/{page.Id}", result.Location);
        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
    }

    [Fact]
    public async Task Update_InvalidBase64Version_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Update(
            1,
            new TalentPagesController.SaveRequest
            {
                JsonLayout = "[]",
                RowVersionBase64 = "not-base64!"
            }));

        Assert.Equal("Invalid page version.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_MissingVersion_ReturnsBadRequest()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Update(
            1,
            new TalentPagesController.SaveRequest
            {
                JsonLayout = "[]",
                RowVersionBase64 = string.Empty
            }));

        Assert.Equal("Page version is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Update_MissingPage_ReturnsNotFound()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        TalentPagesController controller = CreateController(db);

        IActionResult result = await controller.Update(
            999,
            new TalentPagesController.SaveRequest
            {
                JsonLayout = "[]",
                RowVersionBase64 = Convert.ToBase64String(new byte[] { 1 })
            });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsNextVersion()
    {
        await using AppDbContext db = PageBuilderSqliteTestDatabase.CreateContext();
        ContentPage page = Page("holy", "build", "Build", "[]");
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        byte[] originalVersion = page.RowVersion.ToArray();
        TalentPagesController controller = CreateController(db, "talent-admin");

        var result = Assert.IsType<OkObjectResult>(await controller.Update(
            page.Id,
            new TalentPagesController.SaveRequest
            {
                JsonLayout = "[]",
                RowVersionBase64 = Convert.ToBase64String(originalVersion)
            }));

        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal(Convert.ToBase64String(page.RowVersion), ControllerTestSupport.ReadString(result.Value, "rowVersionBase64"));
        Assert.NotEqual(originalVersion, page.RowVersion);
        Assert.Equal("talent-admin", page.UpdatedBy);
    }

    private static TalentPagesController.SaveRequest Request(
        string title,
        string section,
        string slug) => new()
    {
        Title = title,
        Section = section,
        Slug = slug,
        JsonLayout = "[]"
    };

    private static TalentPagesController CreateController(AppDbContext db, string? username = null)
    {
        var validator = new JsonLayoutValidator();
        var service = new TalentPageAdminService(db, validator, new PageService(db, validator));
        var controller = new TalentPagesController(service);
        var claims = string.IsNullOrWhiteSpace(username)
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.Name, username) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test"))
            }
        };
        return controller;
    }

    private static ContentPage Page(
        string section,
        string slug,
        string title,
        string jsonLayout) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = jsonLayout,
        IsPublished = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}
