using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.General;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.General;

public sealed class HomeControllerTests
{
    [Fact]
    public void Home_ReturnsExpectedFrontendContract()
    {
        var controller = new HomeController();

        IActionResult result = controller.Home();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("home", ReadString(ok.Value, "page"));
        Assert.Equal("/Home/Home", ReadString(ok.Value, "frontendRoute"));
    }

    [Fact]
    public void Privacy_ReturnsExpectedFrontendContract()
    {
        var controller = new HomeController();

        IActionResult result = controller.Privacy();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("privacy", ReadString(ok.Value, "page"));
        Assert.Equal("/Home/Privacy", ReadString(ok.Value, "frontendRoute"));
    }

    [Fact]
    public void Discussion_ReturnsExpectedRedirectContract()
    {
        var controller = new HomeController();

        IActionResult result = controller.Discussion();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("/Discussions/Index", ReadString(ok.Value, "redirectUrl"));
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;
}
