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
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Tests.Support;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AuthMultiFactorControllerTests
{
    [Fact]
    public async Task TwoFactor_NoPendingUser_ReturnsExpiredUnauthorized()
    {
        var fixture = CreateFixture();
        fixture.SignIn.Setup(x => x.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync((User?)null);

        IActionResult result = await fixture.Controller.LoginWithTwoFactor(new TwoFactorLoginRequest { Code = "123456" });

        var response = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "The two-factor login session has expired.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task TwoFactor_InvalidFormat_ReturnsBadRequestWithoutSignIn()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);

        IActionResult result = await fixture.Controller.LoginWithTwoFactor(new TwoFactorLoginRequest { Code = "12-34" });

        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Enter a valid 6-digit authenticator code.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
        fixture.SignIn.Verify(
            x => x.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task TwoFactor_NormalizesCodeAndForwardsRememberFlags()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.TwoFactorAuthenticatorSignInAsync("123456", true, true))
            .ReturnsAsync(SignInResult.Failed);

        IActionResult result = await fixture.Controller.LoginWithTwoFactor(new TwoFactorLoginRequest
        {
            Code = "12 34-56",
            RememberMe = true,
            RememberMachine = true
        });

        var response = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid authenticator code.", Assert.IsType<AuthErrorResponse>(response.Value).Message);
        fixture.SignIn.Verify(x => x.TwoFactorAuthenticatorSignInAsync("123456", true, true), Times.Once);
    }

    [Fact]
    public async Task TwoFactor_LockedOut_Returns423()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.TwoFactorAuthenticatorSignInAsync("123456", false, false))
            .ReturnsAsync(SignInResult.LockedOut);

        IActionResult result = await fixture.Controller.LoginWithTwoFactor(new TwoFactorLoginRequest { Code = "123456" });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status423Locked, response.StatusCode);
        Assert.Equal(
            "Your account is temporarily locked.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task TwoFactor_Success_ReturnsAuthenticatedSession()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.Users.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
        fixture.SignIn.Setup(x => x.TwoFactorAuthenticatorSignInAsync("123456", false, true))
            .ReturnsAsync(SignInResult.Success);

        IActionResult result = await fixture.Controller.LoginWithTwoFactor(new TwoFactorLoginRequest
        {
            Code = "123456",
            RememberMachine = true
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(user.Id, session.User?.Id);
    }

    [Fact]
    public async Task Recovery_NoPendingUser_ReturnsExpiredUnauthorized()
    {
        var fixture = CreateFixture();
        fixture.SignIn.Setup(x => x.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync((User?)null);

        IActionResult result = await fixture.Controller.LoginWithRecoveryCode(new RecoveryCodeLoginRequest { RecoveryCode = "abcd" });

        var response = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "The recovery-code login session has expired.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task Recovery_BlankCode_ReturnsBadRequestWithoutSignIn()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);

        IActionResult result = await fixture.Controller.LoginWithRecoveryCode(new RecoveryCodeLoginRequest { RecoveryCode = "   " });

        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Recovery code is required.", Assert.IsType<AuthErrorResponse>(response.Value).Message);
        fixture.SignIn.Verify(x => x.TwoFactorRecoveryCodeSignInAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Recovery_NormalizesSpacesAndReturnsUnauthorizedForInvalidCode()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.TwoFactorRecoveryCodeSignInAsync("abcd"))
            .ReturnsAsync(SignInResult.Failed);

        IActionResult result = await fixture.Controller.LoginWithRecoveryCode(new RecoveryCodeLoginRequest { RecoveryCode = " ab cd " });

        var response = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid recovery code.", Assert.IsType<AuthErrorResponse>(response.Value).Message);
        fixture.SignIn.Verify(x => x.TwoFactorRecoveryCodeSignInAsync("abcd"), Times.Once);
    }

    [Fact]
    public async Task Recovery_LockedOut_Returns423()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.TwoFactorRecoveryCodeSignInAsync("abcd"))
            .ReturnsAsync(SignInResult.LockedOut);

        IActionResult result = await fixture.Controller.LoginWithRecoveryCode(new RecoveryCodeLoginRequest { RecoveryCode = "abcd" });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status423Locked, response.StatusCode);
        Assert.Equal(
            "Your account is temporarily locked.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task Recovery_Success_ReturnsAuthenticatedSession()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.Users.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        fixture.SignIn.Setup(x => x.TwoFactorRecoveryCodeSignInAsync("recovery-code"))
            .ReturnsAsync(SignInResult.Success);

        IActionResult result = await fixture.Controller.LoginWithRecoveryCode(new RecoveryCodeLoginRequest { RecoveryCode = "recovery-code" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.True(session.IsAuthenticated);
        Assert.Contains("Admin", session.User!.Roles);
    }

    private static MultiFactorFixture CreateFixture(User? pendingUser = null)
    {
        Mock<UserManager<User>> users = ControllerTestSupport.CreateUserManager();
        var signIn = new Mock<SignInManager<User>>(
            users.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        if (pendingUser != null)
        {
            signIn.Setup(x => x.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(pendingUser);
        }

        var loginService = new AuthLoginService(
            signIn.Object,
            users.Object,
            new AuthSessionService(users.Object));

        return new MultiFactorFixture(new AuthMultiFactorController(loginService), users, signIn);
    }

    private static User CreateUser() => new()
    {
        Id = "user-1",
        UserName = "viktor",
        Email = "viktor@example.com",
        FullName = "Viktor Iliev"
    };

    private sealed record MultiFactorFixture(
        AuthMultiFactorController Controller,
        Mock<UserManager<User>> Users,
        Mock<SignInManager<User>> SignIn);
}
