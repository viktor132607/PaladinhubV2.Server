using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class PromoCodesControllerTests
{
    [Fact]
    public async Task Index_ReturnsNewestPromoFirst()
    {
        using AppDbContext db = CreateContext();
        db.PromoCodes.AddRange(
            new PromoCode { Id = "old", Code = "OLD", Type = PromoCodeType.Balance, Value = 5m, CreatedAtUtc = new DateTime(2026, 1, 1) },
            new PromoCode { Id = "new", Code = "NEW", Type = PromoCodeType.Balance, Value = 10m, CreatedAtUtc = new DateTime(2026, 2, 1) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, out _);

        IActionResult result = await controller.Index(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsType<List<PromoCode>>(ok.Value);
        Assert.Equal(new[] { "new", "old" }, rows.Select(row => row.Id));
    }

    [Fact]
    public void Create_ReturnsExpectedDefaults()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out _);

        IActionResult result = controller.Create();

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<PromoCode>(ok.Value);
        Assert.Equal(PromoCodeType.Balance, model.Type);
        Assert.Equal(5m, model.Value);
        Assert.Equal("EUR", model.Currency);
        Assert.True(model.IsActive);
    }

    [Fact]
    public async Task CreateApi_NullModel_ReturnsBadRequest()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);

        IActionResult result = await controller.CreateApi(null, TestContext.Current.CancellationToken);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Promo code data is required.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
        service.Verify(x => x.CreateAsync(It.IsAny<PromoCode>()), Times.Never);
    }

    [Fact]
    public async Task CreateApi_InvalidModel_ReturnsValidationProblem()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);
        var model = new PromoCode { Code = " ", Type = PromoCodeType.Balance, Value = 0m };

        IActionResult result = await controller.CreateApi(model, TestContext.Current.CancellationToken);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.Equal(400, details.Status);
        Assert.Contains(nameof(PromoCode.Code), details.Errors.Keys);
        Assert.Contains(nameof(PromoCode.Value), details.Errors.Keys);
        service.Verify(x => x.CreateAsync(It.IsAny<PromoCode>()), Times.Never);
    }

    [Fact]
    public async Task CreateApi_DuplicateNormalizedCode_ReturnsConflict()
    {
        using AppDbContext db = CreateContext();
        db.PromoCodes.Add(new PromoCode { Id = "existing", Code = "SAVE10", Type = PromoCodeType.Balance, Value = 10m });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db, out var service);

        IActionResult result = await controller.CreateApi(
            new PromoCode { Code = " save10 ", Type = PromoCodeType.Balance, Value = 10m, Currency = "eur" },
            TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("Promo code already exists.", ControllerTestSupport.ReadString(conflict.Value, "message"));
        service.Verify(x => x.CreateAsync(It.IsAny<PromoCode>()), Times.Never);
    }

    [Fact]
    public async Task CreateLegacy_ValidModel_NormalizesAndReturnsCreated()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);
        service.Setup(x => x.CreateAsync(It.IsAny<PromoCode>()))
            .ReturnsAsync((PromoCode promo) => promo);

        IActionResult result = await controller.CreateLegacy(
            new PromoCode
            {
                Code = " spring ",
                Type = PromoCodeType.Balance,
                Value = 15m,
                Currency = " eur ",
                Notes = " launch "
            },
            TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(PromoCodesController.Index), created.ActionName);
        var promo = Assert.IsType<PromoCode>(created.Value);
        Assert.Equal("SPRING", promo.Code);
        Assert.Equal("EUR", promo.Currency);
        Assert.Equal("launch", promo.Notes);
        Assert.True(promo.IsActive);
        Assert.Equal(0, promo.UsedCount);
    }

    [Fact]
    public async Task DeactivateApi_BlankId_ReturnsBadRequestWithoutServiceCall()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);

        IActionResult result = await controller.DeactivateApi("   ");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Promo code ID is required.", ControllerTestSupport.ReadString(badRequest.Value, "message"));
        service.Verify(x => x.DeactivateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateLegacy_MissingPromo_ReturnsNotFoundAndTrimsId()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);
        service.Setup(x => x.DeactivateAsync("missing")).ReturnsAsync(false);

        IActionResult result = await controller.DeactivateLegacy(" missing ");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Promo code not found.", ControllerTestSupport.ReadString(notFound.Value, "message"));
        service.Verify(x => x.DeactivateAsync("missing"), Times.Once);
    }

    [Fact]
    public async Task DeactivateApi_Success_ReturnsStableContract()
    {
        using AppDbContext db = CreateContext();
        var controller = CreateController(db, out var service);
        service.Setup(x => x.DeactivateAsync("promo-1")).ReturnsAsync(true);

        IActionResult result = await controller.DeactivateApi(" promo-1 ");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(ControllerTestSupport.ReadBoolean(ok.Value, "ok"));
        Assert.Equal("promo-1", ControllerTestSupport.ReadString(ok.Value, "id"));
        Assert.False(ControllerTestSupport.ReadBoolean(ok.Value, "isActive"));
        Assert.Equal("Promo deactivated.", ControllerTestSupport.ReadString(ok.Value, "message"));
    }

    private static PromoCodesController CreateController(
        AppDbContext db,
        out Mock<IPromoCodeService> service)
    {
        service = new Mock<IPromoCodeService>();
        return new PromoCodesController(db, service.Object);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"promo-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
