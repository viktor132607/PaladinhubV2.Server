using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content.PageBuilder;

public sealed class PageBuilderCreateControllerTests
{
    [Fact]
    public void Create_BuildsNormalizedDefaultModel()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db);

        var result = Assert.IsType<OkObjectResult>(controller.Create(" prot "));
        var model = Assert.IsType<CreatePageViewModel>(result.Value);

        Assert.Equal("Protection", model.Section);
        Assert.Equal(string.Empty, model.Title);
        Assert.Equal(string.Empty, model.Slug);
        Assert.True(model.IsPublished);
        Assert.Equal("[]", model.JsonLayout);
    }

    [Fact]
    public async Task CreateApi_InvalidModelState_ReturnsValidationProblem()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        controller.ModelState.AddModelError("Title", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.CreateApi(Model()));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.True(problem.Errors.ContainsKey("Title"));
        Assert.Empty(db.ContentPages);
    }

    [Fact]
    public async Task CreateLegacy_InvalidModelState_UsesSameValidationPath()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        controller.ModelState.AddModelError("Section", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.CreateLegacy(Model()));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.True(problem.Errors.ContainsKey("Section"));
        Assert.Empty(db.ContentPages);
    }

    [Fact]
    public async Task CreateApi_ExistingRecoverableSlug_ReturnsConflict()
    {
        await using AppDbContext db = CreateContext();
        db.ContentPages.Add(Page("holy", "taken", "Deleted page", isDeleted: true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);
        CreatePageViewModel model = Model();
        model.Section = " Holy ";
        model.Slug = " TAKEN ";

        var result = Assert.IsType<ConflictObjectResult>(await controller.CreateApi(model));

        Assert.Equal("Slug is already used in this section.", ControllerTestSupport.ReadString(result.Value, "message"));
        Assert.Single(db.ContentPages.IgnoreQueryFilters());
    }

    [Fact]
    public async Task CreateApi_BlankSlug_UsesTitleForSlug()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        CreatePageViewModel model = Model();
        model.Section = "Retribution";
        model.Title = "  Burst Guide  ";
        model.Slug = "   ";

        var result = Assert.IsType<CreatedResult>(await controller.CreateApi(model));
        var page = Assert.Single(db.ContentPages);

        Assert.Equal("retribution", page.Section);
        Assert.Equal("burstguide", page.Slug);
        Assert.Equal("Burst Guide", page.Title);
        Assert.Equal("/Retribution/burstguide", result.Location);
    }

    [Fact]
    public async Task CreateApi_ValidRequest_NormalizesAndReturnsCreatedPayload()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        var model = new CreatePageViewModel
        {
            Section = " prot ",
            Title = "  Mythic Guide  ",
            Slug = " My--Guide! ",
            JsonLayout = "  [{\"type\":\"paragraph\"}]  ",
            IsPublished = false
        };

        var result = Assert.IsType<CreatedResult>(await controller.CreateApi(model));
        ContentPage page = Assert.Single(db.ContentPages);

        Assert.Equal("protection", page.Section);
        Assert.Equal("my-guide", page.Slug);
        Assert.Equal("Mythic Guide", page.Title);
        Assert.Equal("[{\"type\":\"paragraph\"}]", page.JsonLayout);
        Assert.True(page.IsPublished);
        Assert.Equal("/Protection/my-guide", result.Location);
        Assert.Equal(page.Id, ControllerTestSupport.ReadInt(result.Value, "id"));
        Assert.Equal("Protection", ControllerTestSupport.ReadString(result.Value, "section"));
        Assert.Equal("/Protection/my-guide", ControllerTestSupport.ReadString(result.Value, "redirectUrl"));
    }

    private static CreatePageViewModel Model() => new()
    {
        Section = "Holy",
        Title = "Guide",
        Slug = "guide",
        JsonLayout = "[]"
    };

    private static ContentPage Page(string section, string slug, string title, bool isDeleted = false) => new()
    {
        Section = section,
        Slug = slug,
        Title = title,
        JsonLayout = "[]",
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        RowVersion = Array.Empty<byte>()
    };

    private static PageBuilderCreateController CreateController(AppDbContext db) =>
        new(new PageBuilderAdminService(db));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"page-builder-create-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
