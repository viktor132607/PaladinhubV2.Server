using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHubV2.Server.API.Controllers.Community;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Community;

public sealed class DiscussionCommentsControllerTests
{
    [Fact]
    public async Task AddComment_WhenContentBlank_ReturnsBadRequest()
    {
        var (controller, discussions, _) = CreateController("user-1");
        var request = new DiscussionCommentsController.AddCommentRequest { Content = "   " };

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(
            await controller.AddComment(Guid.NewGuid(), request));

        Assert.Equal("Comment is required.", ReadString(bad.Value, "message"));
        discussions.Verify(
            service => service.AddCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AddComment_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, discussions, _) = CreateController(null);
        var request = new DiscussionCommentsController.AddCommentRequest { Content = "Comment" };

        IActionResult result = await controller.AddComment(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result);
        discussions.Verify(
            service => service.AddCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AddComment_WhenPostMissing_ReturnsNotFound()
    {
        Guid postId = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.AddCommentAsync(postId, "user-1", "Comment"))
            .ReturnsAsync(false);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.AddComment(
                postId,
                new DiscussionCommentsController.AddCommentRequest { Content = " Comment " }));

        Assert.Equal("Discussion not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task AddComment_WhenSuccessful_TrimsContentAndReturnsOk()
    {
        Guid postId = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.AddCommentAsync(postId, "user-1", "Trim me"))
            .ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.AddComment(
                postId,
                new DiscussionCommentsController.AddCommentRequest { Content = "  Trim me  " }));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        discussions.Verify(service => service.AddCommentAsync(postId, "user-1", "Trim me"), Times.Once);
    }

    [Fact]
    public async Task LikeComment_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, discussions, _) = CreateController(null);

        IActionResult result = await controller.LikeComment(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
        discussions.Verify(service => service.GetCommentByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task LikeComment_WhenCommentMissing_ReturnsNotFound()
    {
        Guid postId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.GetCommentByIdAsync(commentId))
            .ReturnsAsync((DiscussionComment?)null);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(
            await controller.LikeComment(postId, commentId));

        Assert.Equal("Comment not found.", ReadString(notFound.Value, "message"));
        discussions.Verify(
            service => service.ToggleCommentLikeAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LikeComment_WhenCommentBelongsToDifferentPost_ReturnsNotFound()
    {
        Guid postId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        var comment = new DiscussionComment
        {
            Id = commentId,
            PostId = Guid.NewGuid(),
            AuthorId = "owner",
            Content = "Comment"
        };
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.GetCommentByIdAsync(commentId)).ReturnsAsync(comment);

        IActionResult result = await controller.LikeComment(postId, commentId);

        Assert.IsType<NotFoundObjectResult>(result);
        discussions.Verify(
            service => service.ToggleCommentLikeAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LikeComment_WhenToggleFails_ReturnsNotFound()
    {
        Guid postId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        var comment = new DiscussionComment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = "owner",
            Content = "Comment"
        };
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.GetCommentByIdAsync(commentId)).ReturnsAsync(comment);
        discussions.Setup(service => service.ToggleCommentLikeAsync(commentId, "user-1"))
            .ReturnsAsync(false);

        IActionResult result = await controller.LikeComment(postId, commentId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task LikeComment_WhenSuccessful_ReturnsOk()
    {
        Guid postId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        var comment = new DiscussionComment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = "owner",
            Content = "Comment"
        };
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.GetCommentByIdAsync(commentId)).ReturnsAsync(comment);
        discussions.Setup(service => service.ToggleCommentLikeAsync(commentId, "user-1"))
            .ReturnsAsync(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(
            await controller.LikeComment(postId, commentId));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        discussions.Verify(service => service.ToggleCommentLikeAsync(commentId, "user-1"), Times.Once);
    }

    private static (
        DiscussionCommentsController Controller,
        Mock<IDiscussionService> Discussions,
        Mock<UserManager<User>> Users) CreateController(string? userId)
    {
        var discussions = new Mock<IDiscussionService>();
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        var claims = new List<Claim>();
        if (userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        var controller = new DiscussionCommentsController(discussions.Object, users.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId == null ? null : "UnitTest"))
                }
            }
        };

        return (controller, discussions, users);
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
}
