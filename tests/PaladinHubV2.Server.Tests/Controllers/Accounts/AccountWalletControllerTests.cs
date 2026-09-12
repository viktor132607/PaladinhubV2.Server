using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Wallet;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountWalletControllerTests
{
    [Fact]
    public async Task RedeemCode_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _, promo, _) = CreateController(null);

        IActionResult result = await controller.RedeemCode("CODE");

        Assert.IsType<UnauthorizedObjectResult>(result);
        promo.Verify(service => service.RedeemAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedeemCode_WhenCodeIsBlank_ReturnsEmptyReason()
    {
        var (controller, _, promo, _) = CreateController(new User());

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.RedeemCode("  "));

        Assert.False(ReadBoolean(bad.Value, "ok"));
        Assert.Equal("empty", ReadString(bad.Value, "reason"));
        Assert.Equal("Code is required.", ReadString(bad.Value, "message"));
        promo.Verify(service => service.RedeemAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedeemCode_WhenInvalid_ReturnsBadRequestWithInvalidReason()
    {
        var user = new User();
        var (controller, _, promo, _) = CreateController(user);
        promo.Setup(service => service.RedeemAsync(user, "BAD", "USD"))
            .ReturnsAsync((false, "Invalid promo code.", (decimal?)null, (string?)null, (int?)null));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.RedeemCode("BAD"));

        Assert.Equal("invalid", ReadString(bad.Value, "reason"));
        Assert.Equal("Invalid promo code.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task RedeemCode_WhenAlreadyUsed_ReturnsConflict()
    {
        var user = new User();
        var (controller, _, promo, _) = CreateController(user);
        promo.Setup(service => service.RedeemAsync(user, "USED", "USD"))
            .ReturnsAsync((false, "Code already redeemed.", (decimal?)5m, "USD", (int?)null));

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.RedeemCode("USED"));

        Assert.Equal("already-used", ReadString(conflict.Value, "reason"));
        Assert.Equal(5m, ReadDecimal(conflict.Value, "amount"));
    }

    [Fact]
    public async Task RedeemCode_WhenSuccessfulWithoutPercent_DoesNotSetDiscountSession()
    {
        var user = new User();
        var (controller, session, promo, _) = CreateController(user);
        promo.Setup(service => service.RedeemAsync(user, "CASH", "USD"))
            .ReturnsAsync((true, "Redeemed.", (decimal?)10m, "USD", (int?)null));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.RedeemCode("CASH"));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("success", ReadString(ok.Value, "reason"));
        Assert.Null(session.GetInt32("cart_discount_percent"));
    }

    [Fact]
    public async Task RedeemCode_WhenPercentPromo_SetDiscountSession()
    {
        var user = new User();
        var (controller, session, promo, _) = CreateController(user);
        promo.Setup(service => service.RedeemAsync(user, "SAVE20", "USD"))
            .ReturnsAsync((true, "Redeemed.", (decimal?)null, (string?)null, (int?)20));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.RedeemCode("SAVE20"));

        Assert.Equal(20, session.GetInt32("cart_discount_percent"));
        Assert.Equal(20, ReadNullableInt(ok.Value, "percent"));
    }

    [Fact]
    public async Task DevTopUp_WhenAmountIsNotPositive_ReturnsBadRequestBeforeUserLookup()
    {
        var (controller, _, _, wallet) = CreateController(null);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.DevTopUp(0m));

        Assert.Equal("Amount must be greater than zero.", ReadString(bad.Value, "message"));
        wallet.Verify(service => service.TopUpAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DevTopUp_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _, _, wallet) = CreateController(null);

        Assert.IsType<UnauthorizedObjectResult>(await controller.DevTopUp(10m));
        wallet.Verify(service => service.TopUpAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DevTopUp_WhenValid_TopsUpReadsBalanceAndReturnsPayload()
    {
        var user = new User { Id = "user-1" };
        Guid transactionId = Guid.NewGuid();
        var (controller, _, _, wallet) = CreateController(user);
        wallet.Setup(service => service.TopUpAsync("user-1", 25m, "Balance Top-up"))
            .ReturnsAsync(transactionId);
        wallet.Setup(service => service.GetBalanceAsync("user-1"))
            .ReturnsAsync(125m);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.DevTopUp(25m));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal(transactionId, ReadGuid(ok.Value, "transactionId"));
        Assert.Equal(25m, ReadDecimal(ok.Value, "amount"));
        Assert.Equal(125m, ReadDecimal(ok.Value, "balance"));
        Assert.Equal("USD", ReadString(ok.Value, "currency"));
        wallet.Verify(service => service.TopUpAsync("user-1", 25m, "Balance Top-up"), Times.Once);
        wallet.Verify(service => service.GetBalanceAsync("user-1"), Times.Once);
    }

    private static (
        AccountWalletController Controller,
        TestSession Session,
        Mock<IPromoCodeService> Promo,
        Mock<IWalletService> Wallet) CreateController(User? user)
    {
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        var promo = new Mock<IPromoCodeService>();
        var wallet = new Mock<IWalletService>();
        var session = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(session));

        var controller = new AccountWalletController(ui.Object, promo.Object, wallet.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, session, promo, wallet);
    }

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

    private static decimal? ReadDecimal(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as decimal?;

    private static int? ReadNullableInt(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as int?;

    private static Guid ReadGuid(object? value, string propertyName) =>
        (Guid)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? Guid.Empty);

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);
        public bool IsAvailable => true;
        public string Id => "wallet-test-session";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
