using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
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

public sealed class AuthApiControllerTests
{
    [Fact]
    public void GetCsrfToken_ReturnsStoredRequestToken()
    {
        var antiforgery = new Mock<IAntiforgery>();
        var tokens = new AntiforgeryTokenSet(
            "request-token",
            "cookie-token",
            "__RequestVerificationToken",
            "X-CSRF-TOKEN");
        antiforgery
            .Setup(service => service.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(tokens);

        var (userManager, signInManager) = CreateIdentityMocks();
        var controller = CreateController(
            antiforgery.Object,
            userManager.Object,
            signInManager.Object,
            new ClaimsPrincipal(new ClaimsIdentity()));

        IActionResult result = controller.GetCsrfToken();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("request-token", ReadString(ok.Value, "token"));
    }

    [Fact]
    public async Task GetCurrentUser_WhenAnonymous_ReturnsAnonymousSessionWithoutUserLookup()
    {
        var antiforgery = new Mock<IAntiforgery>();
        var (userManager, signInManager) = CreateIdentityMocks();
        var controller = CreateController(
            antiforgery.Object,
            userManager.Object,
            signInManager.Object,
            new ClaimsPrincipal(new ClaimsIdentity()));

        IActionResult result = await controller.GetCurrentUser();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        AuthSessionResponse session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.User);
        userManager.Verify(
            manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()),
            Times.Never);
        signInManager.Verify(manager => manager.SignOutAsync(), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticatedIdentityHasNoUser_SignsOutAndReturnsAnonymous()
    {
        var antiforgery = new Mock<IAntiforgery>();
        var (userManager, signInManager) = CreateIdentityMocks();
        userManager
            .Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((User?)null);
        signInManager
            .Setup(manager => manager.SignOutAsync())
            .Returns(Task.CompletedTask);

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "missing-user") },
                "test"));
        var controller = CreateController(
            antiforgery.Object,
            userManager.Object,
            signInManager.Object,
            principal);

        IActionResult result = await controller.GetCurrentUser();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        AuthSessionResponse session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.User);
        signInManager.Verify(manager => manager.SignOutAsync(), Times.Once);
    }

    private static AuthApiController CreateController(
        IAntiforgery antiforgery,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ClaimsPrincipal user)
    {
        var controller = new AuthApiController(
            antiforgery,
            signInManager,
            userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };

        return controller;
    }

    private static (
        Mock<UserManager<User>> UserManager,
        Mock<SignInManager<User>> SignInManager) CreateIdentityMocks()
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

        return (userManager, signInManager);
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;
}
