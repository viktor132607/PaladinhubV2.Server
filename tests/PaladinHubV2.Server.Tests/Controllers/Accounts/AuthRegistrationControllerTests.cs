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

public sealed class AuthRegistrationControllerTests
{
    [Theory]
    [InlineData("   ", "valid-user", "valid@example.com", "Full name is required.")]
    [InlineData("Valid User", "   ", "valid@example.com", "Username is required.")]
    [InlineData("Valid User", "valid-user", "   ", "Email is required.")]
    public async Task Register_WhenRequiredTrimmedFieldIsEmpty_ReturnsBadRequest(
        string name,
        string username,
        string email,
        string expectedMessage)
    {
        var (controller, users, _, _) = CreateController();
        RegisterRequest request = Request(name, username, email);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Register(request));

        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(bad.Value);
        Assert.Equal(expectedMessage, error.Message);
        users.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenUsernameAlreadyExists_ReturnsConflict()
    {
        var (controller, users, _, _) = CreateController();
        users.Setup(manager => manager.FindByNameAsync("taken-user"))
            .ReturnsAsync(new User { UserName = "taken-user" });

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.Register(Request("Valid User", " taken-user ", "new@example.com")));

        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(conflict.Value);
        Assert.Equal("Username is already taken.", error.Message);
        users.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsConflict()
    {
        var (controller, users, _, _) = CreateController();
        users.Setup(manager => manager.FindByNameAsync("new-user")).ReturnsAsync((User?)null);
        users.Setup(manager => manager.FindByEmailAsync("taken@example.com"))
            .ReturnsAsync(new User { Email = "taken@example.com" });

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(
            await controller.Register(Request("Valid User", "new-user", " taken@example.com ")));

        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(conflict.Value);
        Assert.Equal("Email is already registered.", error.Message);
        users.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenUserCreationFails_ReturnsIdentityErrors()
    {
        var (controller, users, _, signIn) = CreateController();
        SetupNoDuplicates(users);
        users.Setup(manager => manager.CreateAsync(It.IsAny<User>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Password rejected." },
                new IdentityError { Description = "Second error." }));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.Register(Request()));

        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(bad.Value);
        Assert.Equal("Registration failed.", error.Message);
        Assert.Equal(new[] { "Password rejected.", "Second error." }, error.Errors);
        signIn.Verify(manager => manager.SignInAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenDefaultRoleCannotBeCreated_RollsBackUserAndReturnsServerError()
    {
        var (controller, users, roles, signIn) = CreateController();
        SetupNoDuplicates(users);
        users.Setup(manager => manager.CreateAsync(It.IsAny<User>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.DeleteAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);
        roles.SetupSequence(manager => manager.RoleExistsAsync("User"))
            .ReturnsAsync(false)
            .ReturnsAsync(false);
        roles.Setup(manager => manager.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role create failed." }));

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Register(Request()));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(result.Value);
        Assert.Equal("Could not create the default user role.", error.Message);
        Assert.Contains("Role create failed.", error.Errors!);
        users.Verify(manager => manager.DeleteAsync(It.IsAny<User>()), Times.Once);
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        signIn.Verify(manager => manager.SignInAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenRoleAssignmentFails_RollsBackUserAndReturnsServerError()
    {
        var (controller, users, roles, signIn) = CreateController();
        SetupNoDuplicates(users);
        users.Setup(manager => manager.CreateAsync(It.IsAny<User>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<User>(), "User"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));
        users.Setup(manager => manager.DeleteAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);
        roles.Setup(manager => manager.RoleExistsAsync("User")).ReturnsAsync(true);

        ObjectResult result = Assert.IsType<ObjectResult>(await controller.Register(Request()));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        AuthErrorResponse error = Assert.IsType<AuthErrorResponse>(result.Value);
        Assert.Equal("Could not assign the default user role.", error.Message);
        Assert.Contains("Role assignment failed.", error.Errors!);
        users.Verify(manager => manager.DeleteAsync(It.IsAny<User>()), Times.Once);
        signIn.Verify(manager => manager.SignInAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenRoleCreationRacesButRoleThenExists_ContinuesRegistration()
    {
        var (controller, users, roles, signIn) = CreateController();
        SetupNoDuplicates(users);
        users.Setup(manager => manager.CreateAsync(It.IsAny<User>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<User>(), "User"))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.GetRolesAsync(It.IsAny<User>()))
            .ReturnsAsync(new List<string> { "User" });
        roles.SetupSequence(manager => manager.RoleExistsAsync("User"))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        roles.Setup(manager => manager.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Already created elsewhere." }));
        signIn.Setup(manager => manager.SignInAsync(It.IsAny<User>(), false, null))
            .Returns(Task.CompletedTask);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Register(Request()));

        AuthSessionResponse session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.True(session.IsAuthenticated);
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<User>(), "User"), Times.Once);
        signIn.Verify(manager => manager.SignInAsync(It.IsAny<User>(), false, null), Times.Once);
    }

    [Fact]
    public async Task Register_WhenSuccessful_TrimsUserFieldsSignsInAndReturnsSession()
    {
        var (controller, users, roles, signIn) = CreateController();
        SetupNoDuplicates(users, "new-user", "new@example.com");
        User? createdUser = null;
        users.Setup(manager => manager.CreateAsync(It.IsAny<User>(), "Password123!"))
            .Callback<User, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<User>(), "User"))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.GetRolesAsync(It.IsAny<User>()))
            .ReturnsAsync(new List<string> { "User" });
        roles.Setup(manager => manager.RoleExistsAsync("User")).ReturnsAsync(true);
        signIn.Setup(manager => manager.SignInAsync(It.IsAny<User>(), false, null))
            .Returns(Task.CompletedTask);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Register(Request("  New Person  ", "  new-user  ", "  new@example.com  ")));

        Assert.NotNull(createdUser);
        Assert.Equal("New Person", createdUser!.FullName);
        Assert.Equal("new-user", createdUser.UserName);
        Assert.Equal("new@example.com", createdUser.Email);
        Assert.False(createdUser.EmailConfirmed);
        Assert.Matches(@"^/images/avatars/default\d{2}\.png$", createdUser.AvatarPath!);

        AuthSessionResponse session = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.True(session.IsAuthenticated);
        Assert.NotNull(session.User);
        Assert.Equal("new-user", session.User!.Username);
        Assert.Equal("new@example.com", session.User.Email);
        Assert.Equal("New Person", session.User.FullName);
        Assert.Equal(new[] { "User" }, session.User.Roles);
        signIn.Verify(manager => manager.SignInAsync(createdUser, false, null), Times.Once);
    }

    private static RegisterRequest Request(
        string name = "Valid User",
        string username = "valid-user",
        string email = "valid@example.com") => new()
    {
        Name = name,
        Username = username,
        Email = email,
        Password = "Password123!",
        ConfirmPassword = "Password123!"
    };

    private static void SetupNoDuplicates(
        Mock<UserManager<User>> users,
        string username = "valid-user",
        string email = "valid@example.com")
    {
        users.Setup(manager => manager.FindByNameAsync(username)).ReturnsAsync((User?)null);
        users.Setup(manager => manager.FindByEmailAsync(email)).ReturnsAsync((User?)null);
    }

    private static (
        AuthRegistrationController Controller,
        Mock<UserManager<User>> Users,
        Mock<RoleManager<IdentityRole>> Roles,
        Mock<SignInManager<User>> SignIn) CreateController()
    {
        var users = CreateUserManager();
        var roles = CreateRoleManager();
        var signIn = CreateSignInManager(users);

        var controller = new AuthRegistrationController(
            signIn.Object,
            users.Object,
            roles.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, users, roles, signIn);
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

    private static Mock<RoleManager<IdentityRole>> CreateRoleManager() => new(
        Mock.Of<IRoleStore<IdentityRole>>(),
        Array.Empty<IRoleValidator<IdentityRole>>(),
        Mock.Of<ILookupNormalizer>(),
        new IdentityErrorDescriber(),
        NullLogger<RoleManager<IdentityRole>>.Instance);

    private static Mock<SignInManager<User>> CreateSignInManager(Mock<UserManager<User>> users) => new(
        users.Object,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
        Mock.Of<IUserClaimsPrincipalFactory<User>>(),
        Options.Create(new IdentityOptions()),
        NullLogger<SignInManager<User>>.Instance,
        Mock.Of<IAuthenticationSchemeProvider>(),
        Mock.Of<IUserConfirmation<User>>());
}
