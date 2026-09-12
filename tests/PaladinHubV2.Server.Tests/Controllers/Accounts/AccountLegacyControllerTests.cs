using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.Accounts;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class AccountLegacyControllerTests
{
    [Theory]
    [InlineData(nameof(AccountLegacyController.EditProfile), "Profile editing is not implemented yet.")]
    [InlineData(nameof(AccountLegacyController.EditEmail), "Email change is not implemented yet.")]
    [InlineData(nameof(AccountLegacyController.EditPhone), "Phone update is not implemented yet.")]
    [InlineData(nameof(AccountLegacyController.RemovePhone), "Phone removal is not implemented yet.")]
    [InlineData(nameof(AccountLegacyController.EditBattleTag), "BattleTag change is not supported.")]
    [InlineData(nameof(AccountLegacyController.AddAddress), "Address creation is not implemented yet.")]
    [InlineData(nameof(AccountLegacyController.EditAddress), "Address editing is not implemented yet.")]
    public void ParameterlessLegacyActions_Return501WithExpectedMessage(
        string actionName,
        string expectedMessage)
    {
        var controller = new AccountLegacyController();
        var method = typeof(AccountLegacyController).GetMethod(actionName)!;

        ObjectResult result = Assert.IsType<ObjectResult>(method.Invoke(controller, null));

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal(expectedMessage, ReadString(result.Value, "message"));
    }

    [Fact]
    public void ConnectProvider_IncludesProviderInMessage()
    {
        ObjectResult result = Assert.IsType<ObjectResult>(
            new AccountLegacyController().ConnectProvider("GitHub"));

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal(
            "Connecting to GitHub is not implemented yet.",
            ReadString(result.Value, "message"));
    }

    [Fact]
    public void RemoveApp_IncludesApplicationIdInMessage()
    {
        ObjectResult result = Assert.IsType<ObjectResult>(
            new AccountLegacyController().RemoveApp("app-42"));

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal(
            "Removing application app-42 is not implemented yet.",
            ReadString(result.Value, "message"));
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
