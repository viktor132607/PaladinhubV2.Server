using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Tests.Support;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountManagementControllerTests
{
    [Fact]
    public async Task EmailTwoFactor_UnverifiedEmail_CannotEnable()
    {
        var user = new User { EmailConfirmed = false };
        var (controller, users, signIn) = Create(user);
        signIn.Setup(s => s.CheckPasswordSignInAsync(user, "correct", true)).ReturnsAsync(SignInResult.Success);
        Assert.IsType<BadRequestObjectResult>(await controller.EmailTwoFactor(new FactorInput { Password = "correct", Enabled = true }));
        users.Verify(u => u.SetTwoFactorEnabledAsync(It.IsAny<User>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task EmailTwoFactor_WrongPassword_DoesNotChangeFactors()
    {
        var user = new User { EmailConfirmed = true };
        var (controller, users, signIn) = Create(user);
        signIn.Setup(s => s.CheckPasswordSignInAsync(user, "wrong", true)).ReturnsAsync(SignInResult.Failed);
        Assert.IsType<BadRequestObjectResult>(await controller.EmailTwoFactor(new FactorInput { Password = "wrong", Enabled = true }));
        users.Verify(u => u.SetAuthenticationTokenAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Profile_ChangingPhone_ClearsVerification()
    {
        var user = new User { FullName = "Old", PhoneNumber = "+359111111111", PhoneNumberConfirmed = true };
        var (controller, users, _) = Create(user);
        users.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        Assert.IsType<OkObjectResult>(await controller.Profile(new ProfileInput { FullName = "New Name", PhoneNumber = "+359222222222" }));
        Assert.False(user.PhoneNumberConfirmed);
        Assert.Equal("New Name", user.FullName);
    }

    [Fact]
    public async Task SendLoginCode_WithoutPendingLogin_ReturnsUnauthorized()
    {
        var (controller, _, _) = Create(new User());
        Assert.IsType<UnauthorizedObjectResult>(await controller.SendLoginCode());
    }

    [Fact]
    public async Task Forgot_UnknownAccount_DoesNotDiscloseExistence()
    {
        var (controller, _, _) = Create(new User());
        var result = Assert.IsType<OkObjectResult>(await controller.Forgot(new EmailInput { Email = "unknown@example.com" }));
        Assert.Equal("If an eligible account exists, a reset link has been sent.", ControllerTestSupport.ReadString(result.Value, "message"));
    }

    private static (AccountManagementController, Mock<UserManager<User>>, Mock<SignInManager<User>>) Create(User user)
    {
        var users = ControllerTestSupport.CreateUserManager(user);
        var context = ControllerTestSupport.CreateHttpContext(session: new TestSession());
        var signIn = new Mock<SignInManager<User>>(users.Object, new HttpContextAccessor { HttpContext = context },
            Mock.Of<IUserClaimsPrincipalFactory<User>>(), Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance, Mock.Of<IAuthenticationSchemeProvider>(), Mock.Of<IUserConfirmation<User>>());
        var configuration = new ConfigurationBuilder().Build();
        var controller = new AccountManagementController(users.Object, signIn.Object, new AccountEmailService(new HttpClient(), configuration), configuration);
        ControllerTestSupport.Attach(controller, context);
        return (controller, users, signIn);
    }
}
