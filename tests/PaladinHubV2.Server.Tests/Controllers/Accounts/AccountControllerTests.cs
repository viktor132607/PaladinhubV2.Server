using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task MyAccount_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, ui) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.MyAccount(CancellationToken.None));

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
        ui.Verify(
            service => service.BuildMyAccountAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MyAccount_WhenUserExists_ReturnsBuiltModel()
    {
        var user = new User { Id = "user-1" };
        var model = new MyAccountViewModel { Balance = 25m };
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.BuildMyAccountAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.MyAccount(CancellationToken.None));

        Assert.Same(model, result.Value);
        ui.Verify(
            service => service.BuildMyAccountAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Overview_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, ui) = CreateController(null);

        UnauthorizedObjectResult result = Assert.IsType<UnauthorizedObjectResult>(
            await controller.Overview(CancellationToken.None, page: 3));

        Assert.Equal("Authentication required.", ReadString(result.Value, "message"));
        ui.Verify(
            service => service.BuildOverviewAsync(It.IsAny<User>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Overview_WhenUserExists_ForwardsRequestedPage()
    {
        var user = new User { Id = "user-1" };
        var model = new MyAccountViewModel { Page = 3 };
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.BuildOverviewAsync(user, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        OkObjectResult result = Assert.IsType<OkObjectResult>(
            await controller.Overview(CancellationToken.None, page: 3));

        Assert.Same(model, result.Value);
        ui.Verify(
            service => service.BuildOverviewAsync(user, 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Settings_ReturnsNoContent() =>
        Assert.IsType<NoContentResult>(CreateController(null).Controller.Settings());

    [Fact]
    public void AccountDetails_ReturnsNoContent() =>
        Assert.IsType<NoContentResult>(CreateController(null).Controller.AccountDetails());

    [Fact]
    public void Privacy_ReturnsNoContent() =>
        Assert.IsType<NoContentResult>(CreateController(null).Controller.Privacy());

    [Fact]
    public void Connections_ReturnsNoContent() =>
        Assert.IsType<NoContentResult>(CreateController(null).Controller.Connections());

    private static (AccountController Controller, Mock<IAccountUiService> Ui) CreateController(User? user)
    {
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        return (new AccountController(ui.Object), ui);
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
