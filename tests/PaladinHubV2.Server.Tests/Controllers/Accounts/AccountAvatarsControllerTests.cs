using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountAvatarsControllerTests
{
    [Fact]
    public async Task UploadAvatar_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, ui) = CreateController(null);

        IActionResult result = await controller.UploadAvatar(File(), CancellationToken.None);

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        ui.Verify(service => service.UploadAvatarAsync(It.IsAny<User>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAvatar_WhenSuccessful_ReturnsPath()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.UploadAvatarAsync(user, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Success("/uploads/avatar.png"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.UploadAvatar(File(), CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("/uploads/avatar.png", ReadString(ok.Value, "path"));
    }

    [Fact]
    public async Task UploadAvatar_WhenFormatIsUnsupported_Returns415()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.UploadAvatarAsync(user, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Fail(AccountAvatarFailure.UnsupportedFormat, "Unsupported."));

        ObjectResult result = Assert.IsType<ObjectResult>(
            await controller.UploadAvatar(File(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.StatusCode);
        Assert.False(ReadBoolean(result.Value, "ok"));
        Assert.Equal("Unsupported.", ReadString(result.Value, "message"));
    }

    [Fact]
    public async Task UploadAvatar_WhenOtherFailure_ReturnsBadRequest()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.UploadAvatarAsync(user, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Fail(AccountAvatarFailure.NoFile, "No file."));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.UploadAvatar(File(), CancellationToken.None));

        Assert.Equal("No file.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task SetUploadedAvatar_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(null);

        IActionResult result = await controller.SetUploadedAvatar("/a.png", CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SetUploadedAvatar_WhenSuccessful_ReturnsSelectedPath()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.SetUploadedAvatarAsync(user, "/a.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Success("/a.png"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.SetUploadedAvatar("/a.png", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("/a.png", ReadString(ok.Value, "path"));
    }

    [Fact]
    public async Task SetUploadedAvatar_WhenMissing_ReturnsNotFound()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.SetUploadedAvatarAsync(user, "/missing.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Fail(AccountAvatarFailure.NotFound, "Avatar not found."));

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.SetUploadedAvatar("/missing.png", CancellationToken.None));

        Assert.Equal("Avatar not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task DeleteUpload_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(null);

        IActionResult result = await controller.DeleteUpload("/a.png", CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUpload_WhenSuccessful_ReturnsOk()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.DeleteUploadAsync(user, "/a.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Success());

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.DeleteUpload("/a.png", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
    }

    [Fact]
    public async Task DeleteUploadByQuery_WhenPathIsInvalid_ReturnsBadRequest()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.DeleteUploadAsync(user, "bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Fail(AccountAvatarFailure.InvalidPath, "Invalid path."));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.DeleteUploadByQuery("bad", CancellationToken.None));

        Assert.Equal("Invalid path.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task SetDefaultAvatar_WhenUserIsMissing_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(null);

        IActionResult result = await controller.SetDefaultAvatar("default.png", CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SetDefaultAvatar_WhenInvalid_ReturnsBadRequest()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.SetDefaultAvatarAsync(user, "bad.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Fail(AccountAvatarFailure.InvalidDefaultAvatar, "Invalid default avatar."));

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.SetDefaultAvatar("bad.png", CancellationToken.None));

        Assert.False(ReadBoolean(bad.Value, "ok"));
        Assert.Equal("Invalid default avatar.", ReadString(bad.Value, "message"));
    }

    [Fact]
    public async Task SetDefaultAvatar_WhenSuccessful_ReturnsPath()
    {
        var user = new User();
        var (controller, ui) = CreateController(user);
        ui.Setup(service => service.SetDefaultAvatarAsync(user, "default.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Success("/defaults/default.png"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.SetDefaultAvatar("default.png", CancellationToken.None));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal("/defaults/default.png", ReadString(ok.Value, "path"));
    }

    private static (AccountAvatarsController Controller, Mock<IAccountUiService> Ui) CreateController(User? user)
    {
        var ui = new Mock<IAccountUiService>();
        ui.Setup(service => service.GetMe(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var controller = new AccountAvatarsController(ui.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, ui);
    }

    private static IFormFile File()
    {
        var bytes = new byte[] { 1, 2, 3 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "avatar.png");
    }

    private static bool ReadBoolean(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) is true;

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
