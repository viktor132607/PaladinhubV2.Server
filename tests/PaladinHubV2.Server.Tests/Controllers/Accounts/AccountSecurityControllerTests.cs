using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountSecurityControllerTests
{
    [Fact]
    public async Task MarkPhoneVerified_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, ui, security, _) = CreateController(null);

        IActionResult result = await controller.MarkPhoneVerified(CancellationToken.None);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        ui.Verify(
            service => service.MarkPhoneVerifiedAsync(
                It.IsAny<User>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        security.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MarkPhoneVerified_WhenUserExists_MarksPhoneAndReturnsConfirmed()
    {
        var user = new User();
        var (controller, ui, _, _) = CreateController(user);
        ui.Setup(service => service.MarkPhoneVerifiedAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IActionResult result = await controller.MarkPhoneVerified(CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.True(ReadBoolean(ok.Value, "phoneNumberConfirmed"));
        ui.Verify(
            service => service.MarkPhoneVerifiedAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAllDevices_WhenUserExists_DelegatesToSecurityService()
    {
        var user = new User();
        var (controller, _, security, _) = CreateController(user);
        security.Setup(service => service.LogoutAllDevices(user))
            .Returns(Task.CompletedTask);

        IActionResult result = await controller.LogoutAllDevices();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("Logged out from all devices.", ReadString(ok.Value, "message"));
        security.Verify(service => service.LogoutAllDevices(user), Times.Once);
    }

    [Fact]
    public async Task ToggleRequire2FA_WhenEnablingWithoutTwoFactor_ReturnsConflict()
    {
        var user = new User { TwoFactorEnabled = false };
        var (controller, _, _, _) = CreateController(user);

        IActionResult result = await controller.ToggleRequire2FA(true);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(
            "Enable two-factor authentication first.",
            ReadString(conflict.Value, "message"));
    }

    [Theory]
    [InlineData(true, "1", "Authenticator required for login is ON.")]
    [InlineData(false, "0", "Authenticator required for login is OFF.")]
    public async Task ToggleRequire2FA_WhenAllowed_UpdatesSessionAndReturnsState(
        bool enabled,
        string expectedSessionValue,
        string expectedMessage)
    {
        var user = new User { TwoFactorEnabled = true };
        var (controller, _, _, session) = CreateController(user);

        IActionResult result = await controller.ToggleRequire2FA(enabled);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal(enabled, ReadBoolean(ok.Value, "requireTwoFactor"));
        Assert.Equal(expectedMessage, ReadString(ok.Value, "message"));
        Assert.Equal(expectedSessionValue, session.GetString("require_2fa"));
    }

    private static (
        AccountSecurityController Controller,
        Mock<IAccountUiService> Ui,
        Mock<ISecurityService> Security,
        TestSession Session) CreateController(User? user)
    {
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var security = new Mock<ISecurityService>();
        var userManager = CreateUserManager();
        var session = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(session));

        var controller = new AccountSecurityController(
            security.Object,
            ui.Object,
            userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return (controller, ui, security, session);
    }

    private static Mock<UserManager<User>> CreateUserManager() => new(
        Mock.Of<IUserStore<User>>(),
        Options.Create(new IdentityOptions()),
        Mock.Of<IPasswordHasher<User>>(),
        Array.Empty<IUserValidator<User>>(),
        Array.Empty<IPasswordValidator<User>>(),
        Mock.Of<ILookupNormalizer>(),
        new IdentityErrorDescriber(),
        Mock.Of<IServiceProvider>(),
        NullLogger<UserManager<User>>.Instance);

    private static bool ReadBoolean(object? value, string propertyName)
    {
        object? propertyValue = value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value);

        return propertyValue is true;
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id => "unit-test-session";
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
