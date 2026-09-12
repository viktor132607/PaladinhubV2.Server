using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class CartLifecycleControllerTests
{
    [Fact]
    public async Task Cancel_WhenUserIsMissing_ReturnsUnauthorizedWithoutClearingCart()
    {
        var (controller, users, session) = CreateController(null);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(
            await controller.Cancel(CancellationToken.None));

        Assert.False(ReadBoolean(unauthorized.Value, "ok"));
        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        users.Verify(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()), Times.Once);
        session.Verify(
            service => service.CleanAndClear(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_WhenUserExists_CleansCartAndReturnsClearedPayload()
    {
        var user = new User { Id = "user-1" };
        var (controller, _, session) = CreateController(user);
        session.Setup(service => service.CleanAndClear(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.Cancel(CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.True(ReadBoolean(ok.Value, "cleared"));
        Assert.Equal(0m, ReadDecimal(ok.Value, "cartTotal"));
        Assert.Equal("Cart was cleared.", ReadString(ok.Value, "message"));
        session.Verify(
            service => service.CleanAndClear(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (
        CartLifecycleController Controller,
        Mock<UserManager<User>> Users,
        Mock<ICartSessionService> Session) CreateController(User? currentUser)
    {
        Mock<UserManager<User>> users = CreateUserManager();
        users.Setup(manager => manager.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);

        var session = new Mock<ICartSessionService>();
        var controller = new CartLifecycleController(users.Object, session.Object);
        return (controller, users, session);
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

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;

    private static decimal ReadDecimal(object? value, string propertyName) =>
        (decimal)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0m);
}
