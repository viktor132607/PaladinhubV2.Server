using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.GameData;

public sealed class SpellsControllerTests
{
    [Fact]
    public void Create_ReturnsSpellWithDefaultQuality()
    {
        using AppDbContext db = CreateContext();
        var controller = new SpellsController(new SpellAdminService(db));

        IActionResult result = controller.Create();

        var ok = Assert.IsType<OkObjectResult>(result);
        var spell = Assert.IsType<Spell>(ok.Value);
        Assert.Equal(0, spell.Id);
        Assert.Equal("spell", spell.Quality);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Details_InvalidId_ReturnsBadRequest(int id)
    {
        using AppDbContext db = CreateContext();
        var controller = new SpellsController(new SpellAdminService(db));

        IActionResult result = await controller.Details(id, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid spell ID.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
    }

    [Fact]
    public async Task Edit_MissingSpell_ReturnsNotFound()
    {
        using AppDbContext db = CreateContext();
        var controller = new SpellsController(new SpellAdminService(db));

        IActionResult result = await controller.Edit(42, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Spell not found.", ControllerTestSupport.ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task Details_ExistingSpell_ReturnsSpell()
    {
        using AppDbContext db = CreateContext();
        db.Spells.Add(new Spell { Id = 3, Name = "Avenging Wrath" });
        await db.SaveChangesAsync();
        var controller = new SpellsController(new SpellAdminService(db));

        IActionResult result = await controller.Details(3, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var spell = Assert.IsType<Spell>(ok.Value);
        Assert.Equal(3, spell.Id);
        Assert.Equal("Avenging Wrath", spell.Name);
    }

    [Fact]
    public async Task Delete_ExistingSpell_ReturnsSpellWithoutDeletingIt()
    {
        using AppDbContext db = CreateContext();
        db.Spells.Add(new Spell { Id = 9, Name = "Divine Toll" });
        await db.SaveChangesAsync();
        var controller = new SpellsController(new SpellAdminService(db));

        IActionResult result = await controller.Delete(9, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(9, Assert.IsType<Spell>(ok.Value).Id);
        Assert.NotNull(await db.Spells.FindAsync(9));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"spells-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
