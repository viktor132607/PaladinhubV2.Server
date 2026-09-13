using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class ItemMutationsControllerTests
{
    [Fact]
    public async Task Create_InvalidModelState_ReturnsValidationProblem()
    {
        await using AppDbContext db = CreateContext();
        var controller = new ItemMutationsController(db);
        controller.ModelState.AddModelError("Name", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.Create(
            Item("Item"), TestContext.Current.CancellationToken));

        Assert.NotNull(result.Value);
        Assert.Empty(db.Items);
    }

    [Fact]
    public async Task Create_InvalidCategory_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.CategoryId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active category.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidDiscipline_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.DisciplineId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active class or specialization.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidPatch_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.PatchId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Select an active patch.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidRarity_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.RarityId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active rarity.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidMedia_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.Icon = $"/api/spell-icons/{Guid.NewGuid()}";

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active image from the media library.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_InvalidTags_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item item = Item("Item");
        item.TagIds = [999];

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));

        Assert.Equal("Choose existing active tags (up to 100).", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Create_ValidItem_NormalizesAssignmentsAndReturnsCreated()
    {
        await using AppDbContext db = CreateContext();
        Category category = new() { Name = "Consumables" };
        GameDiscipline discipline = new() { Name = "Paladin" };
        GamePatch patch = new() { Name = "11.0" };
        ItemRarity rarity = new() { Name = "Epic", Color = "#a335ee" };
        GameTag tag = new() { Name = "Raid" };
        SpellIcon media = new() { Id = Guid.NewGuid(), Name = "Icon", ContentType = "image/png", Content = [1] };
        db.AddRange(category, discipline, patch, rarity, tag, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var item = new Item
        {
            Id = 123,
            Name = "  Flask  ",
            CategoryId = category.Id,
            DisciplineId = discipline.Id,
            PatchId = patch.Id,
            RarityId = rarity.Id,
            TagIds = [tag.Id, tag.Id],
            Icon = $"  /api/spell-icons/{media.Id}  ",
            SecondIcon = "   ",
            Description = "  Strong  ",
            Url = "  /items/flask  "
        };

        var result = Assert.IsType<CreatedAtActionResult>(await new ItemMutationsController(db).Create(
            item, TestContext.Current.CancellationToken));
        var created = Assert.IsType<Item>(result.Value);

        Assert.True(created.Id > 0);
        Assert.Equal("Flask", created.Name);
        Assert.Equal("Strong", created.Description);
        Assert.Equal("/items/flask", created.Url);
        Assert.Null(created.SecondIcon);
        Assert.Equal("Epic", created.Quality);
        Assert.Equal(new[] { tag.Id }, created.TagIds);
        Assert.Equal("Details", result.ActionName);
        Assert.Equal("Items", result.ControllerName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(4, 5)]
    public async Task Edit_InvalidOrMismatchedId_ReturnsBadRequest(int routeId, int itemId)
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Edit(
            routeId, Item("Item", itemId), TestContext.Current.CancellationToken));
        Assert.Equal("The route ID does not match the item ID.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_InvalidModelState_ReturnsValidationProblem()
    {
        await using AppDbContext db = CreateContext();
        var controller = new ItemMutationsController(db);
        controller.ModelState.AddModelError("Name", "required");

        ObjectResult result = Assert.IsAssignableFrom<ObjectResult>(await controller.Edit(
            1, Item("Item", 1), TestContext.Current.CancellationToken));

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Edit_MissingItem_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await new ItemMutationsController(db).Edit(
            999, Item("Missing", 999), TestContext.Current.CancellationToken));
        Assert.Equal("Item not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_InvalidAssignment_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        Item existing = Item("Existing");
        db.Items.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Item update = Item("Existing", existing.Id);
        update.CategoryId = 999;

        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).Edit(
            existing.Id, update, TestContext.Current.CancellationToken));

        Assert.Equal("Choose an active category.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Edit_KeepingArchivedPreviousCategory_IsAllowedAndNormalizesValues()
    {
        await using AppDbContext db = CreateContext();
        Category category = new() { Name = "Legacy", IsArchived = true };
        db.Categories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Item existing = Item("Old");
        existing.CategoryId = category.Id;
        db.Items.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Item update = Item("  New  ", existing.Id);
        update.CategoryId = category.Id;
        update.Description = "  Updated  ";

        var result = Assert.IsType<OkObjectResult>(await new ItemMutationsController(db).Edit(
            existing.Id, update, TestContext.Current.CancellationToken));
        var saved = Assert.IsType<Item>(result.Value);

        Assert.Equal("New", saved.Name);
        Assert.Equal("Updated", saved.Description);
        Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task Delete_InvalidId_ReturnsBadRequest()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<BadRequestObjectResult>(await new ItemMutationsController(db).DeleteConfirmed(
            0, TestContext.Current.CancellationToken));
        Assert.Equal("Invalid item ID.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_MissingItem_ReturnsNotFound()
    {
        await using AppDbContext db = CreateContext();
        var result = Assert.IsType<NotFoundObjectResult>(await new ItemMutationsController(db).DeleteConfirmed(
            999, TestContext.Current.CancellationToken));
        Assert.Equal("Item not found.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Delete_ExistingItem_ReturnsNoContentAndRemovesIt()
    {
        await using AppDbContext db = CreateContext();
        Item existing = Item("Existing");
        db.Items.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IActionResult result = await new ItemMutationsController(db).DeleteConfirmed(
            existing.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Items);
    }

    private static Item Item(string name, int id = 0) => new()
    {
        Id = id,
        Name = name,
        TagIds = []
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"item-mutations-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
