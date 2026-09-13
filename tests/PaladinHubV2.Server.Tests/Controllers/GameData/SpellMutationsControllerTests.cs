using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class SpellMutationsControllerTests
{
    [Fact]
    public async Task Create_NullSpell_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            null, TestContext.Current.CancellationToken));
        Assert.Equal("Spell data is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidModelState_ReturnsValidationProblem()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        controller.ModelState.AddModelError("Name", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.Create(
            Spell("Spell"), TestContext.Current.CancellationToken));

        Assert.NotNull(result.Value);
        Assert.Empty(db.Spells);
    }

    [Fact]
    public async Task Create_InvalidCategory_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.CategoryId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active category.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidDiscipline_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.DisciplineId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active class or specialization.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidPatch_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.PatchId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Select an active patch.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidMedia_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.Icon = $"/api/spell-icons/{Guid.NewGuid()}";

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active image from the media library.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidTags_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.TagIds = [999];

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Choose existing active tags (up to 100).", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_MissingRecordType_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell spell = Spell("Spell");
        spell.Quality = "talent";

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an existing type. Refresh the type list if it was changed.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidSpell_NormalizesAndReturnsCreated()
    {
        await using AppDbContext db = CreateContext();
        Category category = new() { Name = "Abilities" };
        GameDiscipline discipline = new() { Name = "Paladin" };
        GamePatch patch = new() { Name = "11.0" };
        GameTag tag = new() { Name = "Holy" };
        SpellIcon media = new() { Id = Guid.NewGuid(), Name = "Icon", ContentType = "image/png", Content = [1] };
        db.AddRange(category, discipline, patch, tag, media, new RecordType { Name = "spell" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var spell = new Spell
        {
            Id = 44,
            Name = "  Holy Shock  ",
            CategoryId = category.Id,
            DisciplineId = discipline.Id,
            PatchId = patch.Id,
            TagIds = [tag.Id, tag.Id],
            Icon = $"/api/spell-icons/{media.Id}",
            Description = "  Heals or damages  ",
            Url = "   ",
            Quality = " SPELL "
        };

        var result = Assert.IsType<CreatedAtActionResult>(await CreateController(db).Create(
            spell, TestContext.Current.CancellationToken));
        var created = Assert.IsType<Spell>(result.Value);

        Assert.True(created.Id > 0);
        Assert.Equal("Holy Shock", created.Name);
        Assert.Equal("Heals or damages", created.Description);
        Assert.Null(created.Url);
        Assert.Equal("spell", created.Quality);
        Assert.Equal(new[] { tag.Id }, created.TagIds);
        Assert.Equal("Details", result.ActionName);
        Assert.Equal("Spells", result.ControllerName);
    }

    [Fact]
    public async Task Edit_InvalidId_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            0, Spell("Spell"), TestContext.Current.CancellationToken));
        Assert.Equal("Invalid spell ID.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_NullSpell_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            1, null, TestContext.Current.CancellationToken));
        Assert.Equal("Spell data is required.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_MismatchedId_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            1, Spell("Spell", 2), TestContext.Current.CancellationToken));
        Assert.Equal("The route ID does not match the spell ID.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_InvalidModelState_ReturnsValidationProblem()
    {
        await using AppDbContext db = CreateContext();
        var controller = CreateController(db);
        controller.ModelState.AddModelError("Name", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.Edit(
            1, Spell("Spell", 1), TestContext.Current.CancellationToken));

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Edit_MissingSpell_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).Edit(
            999, Spell("Missing", 999), TestContext.Current.CancellationToken));
        Assert.Equal("Spell not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_InvalidAssignment_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell existing = Spell("Existing");
        db.Spells.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Spell update = Spell("Existing", existing.Id);
        update.PatchId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            existing.Id, update, TestContext.Current.CancellationToken));

        Assert.Equal("Select an active patch.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_MissingRecordType_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Spell existing = Spell("Existing");
        db.Spells.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Spell update = Spell("Existing", existing.Id);
        update.Quality = "talent";

        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).Edit(
            existing.Id, update, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an existing type. Refresh the type list if it was changed.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_KeepingArchivedPreviousCategory_IsAllowed()
    {
        await using AppDbContext db = CreateContext();
        Category category = new() { Name = "Legacy", IsArchived = true };
        db.Categories.Add(category);
        db.RecordTypes.Add(new RecordType { Name = "spell" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Spell existing = Spell("Old");
        existing.CategoryId = category.Id;
        db.Spells.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Spell update = Spell("  New  ", existing.Id);
        update.CategoryId = category.Id;
        update.Description = "  Updated  ";

        var result = Assert.IsType<OkObjectResult>(await CreateController(db).Edit(
            existing.Id, update, TestContext.Current.CancellationToken));
        var saved = Assert.IsType<Spell>(result.Value);

        Assert.Equal("New", saved.Name);
        Assert.Equal("Updated", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task Delete_InvalidId_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await CreateController(db).DeleteConfirmed(
            -1, TestContext.Current.CancellationToken));
        Assert.Equal("Invalid spell ID.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_MissingSpell_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await CreateController(db).DeleteConfirmed(
            999, TestContext.Current.CancellationToken));
        Assert.Equal("Spell not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_ExistingSpell_ReturnsNoContentAndRemovesIt()
    {
        await using AppDbContext db = CreateContext();
        Spell existing = Spell("Existing");
        db.Spells.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IActionResult result = await CreateController(db).DeleteConfirmed(
            existing.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Spells);
    }

    private static SpellMutationsController CreateController(AppDbContext db) =>
        new(new SpellAdminService(db));

    private static Spell Spell(string name, int id = 0) => new()
    {
        Id = id,
        Name = name,
        Quality = "spell",
        TagIds = []
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"spell-mutations-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
