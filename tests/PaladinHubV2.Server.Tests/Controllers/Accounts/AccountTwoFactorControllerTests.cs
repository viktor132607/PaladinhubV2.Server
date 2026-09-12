using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Tests.Support;
using Xunit;
using static PaladinHubV2.Server.Tests.Support.ControllerTestSupport;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountTwoFactorControllerTests
{
    [Fact]
    public async Task Enable2FAGet_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, _, _) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.Enable2FA(reset: false));

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAGet_WhenAlreadyEnabled_ReturnsEnabledState()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = true };
        var (controller, _, _, _) = CreateController(user);

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.Enable2FA(reset: false));

        Assert.True(ReadBoolean(result.Value, "twoFactorEnabled"));
        Assert.Null(Read(result.Value, "sharedKey"));
    }

    [Fact]
    public async Task Enable2FAGet_WhenResetRequestedWhileEnabled_ReturnsConflict()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = true };
        var (controller, _, _, _) = CreateController(user);

        ConflictObjectResult result = Assert.IsType<ConflictObjectResult>(
            await controller.Enable2FA(reset: true));

        Assert.Equal(
            "Disable two-factor authentication before resetting the authenticator key.",
            ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAGet_WithExistingKey_ReturnsFormattedSetupData()
    {
        var user = new User { Id = "user-1", Email = "viktor@example.com" };
        var (controller, _, users, _) = CreateController(user);
        users.Setup(manager => manager.GetAuthenticatorKeyAsync(user)).ReturnsAsync("abcd1234");

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.Enable2FA(reset: false));

        Assert.False(ReadBoolean(result.Value, "twoFactorEnabled"));
        Assert.Equal("ABCD 1234", ReadString(result.Value, "sharedKey"));
        Assert.StartsWith("otpauth://totp/PaladinHub:", ReadString(result.Value, "authenticatorUri"));
        Assert.Contains("secret=abcd1234", ReadString(result.Value, "authenticatorUri"));
        Assert.StartsWith(
            "https://api.qrserver.com/v1/create-qr-code/",
            ReadString(result.Value, "qrCodeUrl"));
    }

    [Fact]
    public async Task Enable2FAGet_WhenKeyResetFails_ReturnsServerErrorWithErrors()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, users, _) = CreateController(user);
        users.Setup(manager => manager.GetAuthenticatorKeyAsync(user)).ReturnsAsync((string?)null);
        users.Setup(manager => manager.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(
            IdentityResult.Failed(new IdentityError { Description = "reset failed" }));

        ObjectResult result = Assert.IsType<ObjectResult>(
            await controller.Enable2FA(reset: false));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Authenticator key could not be generated.", ReadString(result.Value, "message"));
        string[] errors = Assert.IsType<string[]>(Read(result.Value, "errors"));
        Assert.Equal(new[] { "reset failed" }, errors);
    }

    [Fact]
    public async Task Enable2FAGet_WhenKeyStillMissingAfterReset_ReturnsServerError()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, users, _) = CreateController(user);
        users.SetupSequence(manager => manager.GetAuthenticatorKeyAsync(user))
            .ReturnsAsync((string?)null)
            .ReturnsAsync((string?)null);
        users.Setup(manager => manager.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);

        ObjectResult result = Assert.IsType<ObjectResult>(
            await controller.Enable2FA(reset: false));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Authenticator key could not be loaded.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAPost_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, _, _) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.Enable2FA("123456"));

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAPost_WhenAlreadyEnabled_ReturnsConflict()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = true };
        var (controller, _, _, _) = CreateController(user);

        ConflictObjectResult result = Assert.IsType<ConflictObjectResult>(
            await controller.Enable2FA("123456"));

        Assert.Equal(
            "Two-factor authentication is already enabled.",
            ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAPost_WhenCodeFormatInvalid_ReturnsBadRequest()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, _, _) = CreateController(user);

        BadRequestObjectResult result = Assert.IsType<BadRequestObjectResult>(
            await controller.Enable2FA("12-34"));

        Assert.Equal(
            "Enter a valid 6-digit authenticator code.",
            ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAPost_WhenTokenInvalid_ReturnsBadRequest()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, users, _) = CreateController(user);
        users.Setup(manager => manager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                "123456"))
            .ReturnsAsync(false);

        BadRequestObjectResult result = Assert.IsType<BadRequestObjectResult>(
            await controller.Enable2FA("123 456"));

        Assert.Equal("Invalid authenticator code.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Enable2FAPost_WhenValid_EnablesAndReturnsRecoveryCodes()
    {
        var user = new User { Id = "user-1" };
        var (controller, security, users, _) = CreateController(user);
        users.Setup(manager => manager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                "123456"))
            .ReturnsAsync(true);
        users.Setup(manager => manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))
            .ReturnsAsync(new[] { "code-1", "code-2" });
        security.Setup(service => service.ToggleTwoFactor(user, true)).Returns(Task.CompletedTask);

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.Enable2FA("123-456"));

        Assert.True(ReadBoolean(result.Value, "ok"));
        Assert.True(ReadBoolean(result.Value, "twoFactorEnabled"));
        Assert.Equal(
            new[] { "code-1", "code-2" },
            Assert.IsType<string[]>(Read(result.Value, "recoveryCodes")));
        security.Verify(service => service.ToggleTwoFactor(user, true), Times.Once);
    }

    [Fact]
    public async Task Disable2FA_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, _, _) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.Disable2FA());

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task Disable2FA_WhenEnabled_DisablesAndClearsRequirementFlag()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = true };
        var (controller, security, _, session) = CreateController(user);
        security.Setup(service => service.ToggleTwoFactor(user, false)).Returns(Task.CompletedTask);

        OkObjectResult result = Assert.IsType<OkObjectResult>(await controller.Disable2FA());

        Assert.True(ReadBoolean(result.Value, "ok"));
        Assert.False(ReadBoolean(result.Value, "twoFactorEnabled"));
        Assert.False(ReadBoolean(result.Value, "requireTwoFactor"));
        Assert.Equal("0", session.GetString("require_2fa"));
        security.Verify(service => service.ToggleTwoFactor(user, false), Times.Once);
    }

    [Fact]
    public async Task GenerateRecoveryCode_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, _, _, _) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.GenerateRecoveryCode());

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task GenerateRecoveryCode_WhenTwoFactorDisabled_ReturnsConflict()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = false };
        var (controller, _, _, _) = CreateController(user);

        ConflictObjectResult result = Assert.IsType<ConflictObjectResult>(
            await controller.GenerateRecoveryCode());

        Assert.Equal(
            "Enable two-factor authentication before generating recovery codes.",
            ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task GenerateRecoveryCode_WhenEnabled_ReturnsNewCodes()
    {
        var user = new User { Id = "user-1", TwoFactorEnabled = true };
        var (controller, _, users, _) = CreateController(user);
        users.Setup(manager => manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))
            .ReturnsAsync(new[] { "new-1", "new-2" });

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.GenerateRecoveryCode());

        Assert.True(ReadBoolean(result.Value, "ok"));
        Assert.Equal(
            new[] { "new-1", "new-2" },
            Assert.IsType<string[]>(Read(result.Value, "recoveryCodes")));
    }

    private static (
        AccountTwoFactorController Controller,
        Mock<ISecurityService> Security,
        Mock<UserManager<User>> Users,
        TestSession Session) CreateController(User? user)
    {
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var security = new Mock<ISecurityService>();
        var users = CreateUserManager();
        var session = new TestSession();
        var twoFactor = new AccountTwoFactorService(security.Object, users.Object);
        var controller = new AccountTwoFactorController(ui.Object, twoFactor);
        Attach(controller, CreateHttpContext(session: session));

        return (controller, security, users, session);
    }
}
