using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AuthCredentialsControllerTests
{
    [Fact]
    public async Task Logout_ReturnsAnonymousSessionAndSignsOut()
    {
        var (controller, userManager, signInManager) = CreateController();
        signInManager
            .Setup(manager => manager.SignOutAsync())
            .Returns(Task.CompletedTask);

        IActionResult result = await controller.Logout();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        AuthSessionResponse session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.User);
        signInManager.Verify(manager => manager.SignOutAsync(), Times.Once);
        userManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyAccountLogout_PreservesOkTrueContractAndSignsOut()
    {
        var (controller, _, signInManager) = CreateController();
        signInManager
            .Setup(manager => manager.SignOutAsync())
            .Returns(Task.CompletedTask);

        IActionResult result = await controller.LegacyAccountLogout();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        signInManager.Verify(manager => manager.SignOutAsync(), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, userManager, signInManager) = CreateController();
        userManager
            .Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((User?)null);

        IActionResult result = await controller.ChangePassword(Request());

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(unauthorized.Value);
        Assert.Equal("Authentication required.", error.Message);
        signInManager.Verify(
            manager => manager.RefreshSignInAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenIdentityRejectsPassword_ReturnsErrors()
    {
        var user = new User();
        var (controller, userManager, signInManager) = CreateController();
        userManager
            .Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManager
            .Setup(manager => manager.ChangePasswordAsync(user, "OldPassword1!", "NewPassword2!"))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Password policy failed." }));

        IActionResult result = await controller.ChangePassword(Request());

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(badRequest.Value);
        Assert.Equal("Password update failed.", error.Message);
        Assert.Equal(new[] { "Password policy failed." }, error.Errors);
        signInManager.Verify(
            manager => manager.RefreshSignInAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenIdentitySucceeds_RefreshesSignInAndReturnsMessage()
    {
        var user = new User();
        var (controller, userManager, signInManager) = CreateController();
        userManager
            .Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManager
            .Setup(manager => manager.ChangePasswordAsync(user, "OldPassword1!", "NewPassword2!"))
            .ReturnsAsync(IdentityResult.Success);
        signInManager
            .Setup(manager => manager.RefreshSignInAsync(user))
            .Returns(Task.CompletedTask);

        IActionResult result = await controller.ChangePassword(Request());

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Your password has been updated.", ReadString(ok.Value, "message"));
        signInManager.Verify(manager => manager.RefreshSignInAsync(user), Times.Once);
    }

    private static ChangePasswordRequest Request() => new()
    {
        OldPassword = "OldPassword1!",
        NewPassword = "NewPassword2!",
        ConfirmNewPassword = "NewPassword2!"
    };

    private static (
        AuthCredentialsController Controller,
        Mock<UserManager<User>> UserManager,
        Mock<SignInManager<User>> SignInManager) CreateController()
    {
        var userManager = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            NullLogger<UserManager<User>>.Instance);

        var signInManager = new Mock<SignInManager<User>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        var controller = new AuthCredentialsController(
            signInManager.Object,
            userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, userManager, signInManager);
    }

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
}
