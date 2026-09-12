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

public sealed class AuthLoginControllerTests
{
    [Fact]
    public async Task Login_BlankIdentifier_ReturnsUnauthorizedWithoutLookup()
    {
        var fixture = CreateFixture();

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "   ",
            Password = "password"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Email/username or password is incorrect.",
            Assert.IsType<AuthErrorResponse>(unauthorized.Value).Message);
        fixture.Users.Verify(x => x.FindByNameAsync(It.IsAny<string>()), Times.Never);
        fixture.SignIn.Verify(
            x => x.PasswordSignInAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_UnknownIdentifier_TrimsAndFallsBackToEmail()
    {
        var fixture = CreateFixture();
        fixture.Users.Setup(x => x.FindByNameAsync("user@example.com")).ReturnsAsync((User?)null);
        fixture.Users.Setup(x => x.FindByEmailAsync("user@example.com")).ReturnsAsync((User?)null);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "  user@example.com  ",
            Password = "password"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
        fixture.Users.Verify(x => x.FindByNameAsync("user@example.com"), Times.Once);
        fixture.Users.Verify(x => x.FindByEmailAsync("user@example.com"), Times.Once);
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_ReturnsAcceptedWithRememberMe()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.PasswordSignInAsync(user, "password", true, true))
            .ReturnsAsync(SignInResult.TwoFactorRequired);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "  viktor  ",
            Password = "password",
            RememberMe = true
        });

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.True(ControllerTestSupport.ReadBoolean(accepted.Value, "requiresTwoFactor"));
        Assert.True(ControllerTestSupport.ReadBoolean(accepted.Value, "rememberMe"));
        fixture.Users.Verify(x => x.FindByNameAsync("viktor"), Times.Once);
    }

    [Fact]
    public async Task Login_LockedOut_Returns423()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.PasswordSignInAsync(user, "password", false, true))
            .ReturnsAsync(SignInResult.LockedOut);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "viktor",
            Password = "password"
        });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status423Locked, response.StatusCode);
        Assert.Equal(
            "Your account is temporarily locked.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task Login_NotAllowed_ReturnsForbidden()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.PasswordSignInAsync(user, "password", false, true))
            .ReturnsAsync(SignInResult.NotAllowed);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "viktor",
            Password = "password"
        });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(
            "Login is not allowed for this account.",
            Assert.IsType<AuthErrorResponse>(response.Value).Message);
    }

    [Fact]
    public async Task Login_FailedPassword_ReturnsUnauthorized()
    {
        var user = CreateUser();
        var fixture = CreateFixture(user);
        fixture.SignIn.Setup(x => x.PasswordSignInAsync(user, "wrong", false, true))
            .ReturnsAsync(SignInResult.Failed);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "viktor",
            Password = "wrong"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(
            "Email/username or password is incorrect.",
            Assert.IsType<AuthErrorResponse>(unauthorized.Value).Message);
    }

    [Fact]
    public async Task Login_EmailFallbackSuccess_ReturnsAuthenticatedSession()
    {
        var user = CreateUser();
        var fixture = CreateFixture();
        fixture.Users.Setup(x => x.FindByNameAsync("viktor@example.com")).ReturnsAsync((User?)null);
        fixture.Users.Setup(x => x.FindByEmailAsync("viktor@example.com")).ReturnsAsync(user);
        fixture.Users.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        fixture.SignIn.Setup(x => x.PasswordSignInAsync(user, "password", true, true))
            .ReturnsAsync(SignInResult.Success);

        IActionResult result = await fixture.Controller.Login(new LoginRequest
        {
            Identifier = "viktor@example.com",
            Password = "password",
            RememberMe = true
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.True(session.IsAuthenticated);
        Assert.NotNull(session.User);
        Assert.Equal(user.Id, session.User.Id);
        Assert.Equal("viktor", session.User.Username);
        Assert.Equal("viktor@example.com", session.User.Email);
        Assert.Equal(new[] { "Admin" }, session.User.Roles);
    }

    private static LoginFixture CreateFixture(User? user = null)
    {
        Mock<UserManager<User>> users = ControllerTestSupport.CreateUserManager();
        if (user != null)
        {
            users.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
        }

        var signIn = new Mock<SignInManager<User>>(
            users.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        var sessionService = new AuthSessionService(users.Object);
        var loginService = new AuthLoginService(signIn.Object, users.Object, sessionService);
        return new LoginFixture(new AuthLoginController(loginService), users, signIn);
    }

    private static User CreateUser() => new()
    {
        Id = "user-1",
        UserName = "viktor",
        Email = "viktor@example.com",
        FullName = "Viktor Iliev"
    };

    private sealed record LoginFixture(
        AuthLoginController Controller,
        Mock<UserManager<User>> Users,
        Mock<SignInManager<User>> SignIn);
}
