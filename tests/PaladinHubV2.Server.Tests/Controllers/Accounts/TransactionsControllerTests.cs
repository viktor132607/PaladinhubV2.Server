using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaladinHub.Models;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Transactions;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Accounts;

public sealed class TransactionsControllerTests
{
    [Fact]
    public async Task TransactionHistory_WhenUserIdMissing_ReturnsUnauthorized()
    {
        var transactions = new Mock<ITransactionsService>();
        var account = new Mock<IAccountUiService>();
        account
            .Setup(service => service.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns((string?)null);
        var controller = CreateController(transactions.Object, account.Object);

        IActionResult result = await controller.TransactionHistory();

        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Authentication required.", ReadString(unauthorized.Value, "message"));
        transactions.Verify(
            service => service.GetHistory(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task Transactions_NormalizesPagePageSizeAndRegionBeforeCallingService()
    {
        var transactions = new Mock<ITransactionsService>();
        var account = new Mock<IAccountUiService>();
        account
            .Setup(service => service.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns("user-1");
        transactions
            .Setup(service => service.GetHistory("user-1", "Europe", 1, 100))
            .ReturnsAsync((TransactionHistoryViewModel)null!);
        var controller = CreateController(transactions.Object, account.Object);

        IActionResult result = await controller.Transactions(
            page: 0,
            pageSize: 500,
            region: "   ");

        Assert.IsType<OkObjectResult>(result);
        transactions.Verify(
            service => service.GetHistory("user-1", "Europe", 1, 100),
            Times.Once);
    }

    [Fact]
    public async Task TransactionHistory_TrimsCustomRegionAndKeepsValidPaging()
    {
        var transactions = new Mock<ITransactionsService>();
        var account = new Mock<IAccountUiService>();
        account
            .Setup(service => service.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns("user-2");
        transactions
            .Setup(service => service.GetHistory("user-2", "North America", 3, 25))
            .ReturnsAsync((TransactionHistoryViewModel)null!);
        var controller = CreateController(transactions.Object, account.Object);

        IActionResult result = await controller.TransactionHistory(
            page: 3,
            pageSize: 25,
            region: "  North America  ");

        Assert.IsType<OkObjectResult>(result);
        transactions.Verify(
            service => service.GetHistory("user-2", "North America", 3, 25),
            Times.Once);
    }

    private static TransactionsController CreateController(
        ITransactionsService transactions,
        IAccountUiService account)
    {
        return new TransactionsController(transactions, account)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static string? ReadString(object? value, string propertyName) =>
        value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;
}
