using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.API.Controllers.Community;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Community;

public sealed class DiscussionPostsControllerTests
{
    [Fact]
    public async Task Create_WhenTitleAndContentBlank_ReturnsValidationProblem()
    {
        var (controller, discussions, _) = CreateController("user-1");
        var model = new CreatePostViewModel { Title = "   ", Content = "   " };

        ObjectResult validation = Assert.IsAssignableFrom<ObjectResult>(await controller.Create(model));
        ValidationProblemDetails details = Assert.IsType<ValidationProblemDetails>(validation.Value);

        Assert.True(details.Errors.ContainsKey(nameof(model.Title)));
        Assert.True(details.Errors.ContainsKey(nameof(model.Content)));
        discussions.Verify(service => service.CreateAsync(It.IsAny<string>(), It.IsAny<CreatePostViewModel>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenUserMissing_ReturnsUnauthorized()
    {
        var (controller, discussions, _) = CreateController(null);
        var model = new CreatePostViewModel { Title = "Title", Content = "Content" };

        IActionResult result = await controller.Create(model);

        Assert.IsType<UnauthorizedObjectResult>(result);
        discussions.Verify(service => service.CreateAsync(It.IsAny<string>(), It.IsAny<CreatePostViewModel>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenValid_TrimsAndDelegates()
    {
        var (controller, discussions, _) = CreateController("user-1");
        var model = new CreatePostViewModel
        {
            Title = "  Hello Paladins  ",
            Content = "  Body text  "
        };
        discussions.Setup(service => service.CreateAsync("user-1", model))
            .Returns(Task.CompletedTask);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Create(model));

        Assert.Equal("Hello Paladins", model.Title);
        Assert.Equal("Body text", model.Content);
        Assert.True(ReadBoolean(ok.Value, "ok"));
        discussions.Verify(service => service.CreateAsync("user-1", model), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenDiscussionMissing_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync((DiscussionPost?)null);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(await controller.Delete(id));

        Assert.Equal("Discussion not found.", ReadString(notFound.Value, "message"));
        discussions.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenUserMissing_ReturnsUnauthorized()
    {
        Guid id = Guid.NewGuid();
        var post = new DiscussionPost { Id = id, AuthorId = "owner", Title = "T", Content = "C" };
        var (controller, discussions, _) = CreateController(null);
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync(post);

        IActionResult result = await controller.Delete(id);

        Assert.IsType<UnauthorizedObjectResult>(result);
        discussions.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenUserIsNotOwnerOrAdmin_ReturnsForbid()
    {
        Guid id = Guid.NewGuid();
        var post = new DiscussionPost { Id = id, AuthorId = "owner", Title = "T", Content = "C" };
        var (controller, discussions, _) = CreateController("other-user");
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync(post);

        IActionResult result = await controller.Delete(id);

        Assert.IsType<ForbidResult>(result);
        discussions.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenOwner_DeletesAndReturnsNoContent()
    {
        Guid id = Guid.NewGuid();
        var post = new DiscussionPost { Id = id, AuthorId = "owner", Title = "T", Content = "C" };
        var (controller, discussions, _) = CreateController("owner");
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync(post);
        discussions.Setup(service => service.DeleteAsync(id, "owner", false)).ReturnsAsync(true);

        IActionResult result = await controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        discussions.Verify(service => service.DeleteAsync(id, "owner", false), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenAdmin_DeletesOtherUsersPost()
    {
        Guid id = Guid.NewGuid();
        var post = new DiscussionPost { Id = id, AuthorId = "owner", Title = "T", Content = "C" };
        var (controller, discussions, _) = CreateController("admin-user", isAdmin: true);
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync(post);
        discussions.Setup(service => service.DeleteAsync(id, "admin-user", true)).ReturnsAsync(true);

        IActionResult result = await controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        discussions.Verify(service => service.DeleteAsync(id, "admin-user", true), Times.Once);
    }

    [Fact]
    public async Task Like_WhenUserMissing_ReturnsUnauthorized()
    {
        Guid id = Guid.NewGuid();
        var (controller, discussions, _) = CreateController(null);

        IActionResult result = await controller.Like(id);

        Assert.IsType<UnauthorizedObjectResult>(result);
        discussions.Verify(service => service.ToggleLikeAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Like_WhenDiscussionMissing_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.ToggleLikeAsync(id, "user-1")).ReturnsAsync(false);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(await controller.Like(id));

        Assert.Equal("Discussion not found.", ReadString(notFound.Value, "message"));
        discussions.Verify(service => service.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Like_WhenPostReloadReturnsNull_ReturnsZeroLikesAndNotLiked()
    {
        Guid id = Guid.NewGuid();
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.ToggleLikeAsync(id, "user-1")).ReturnsAsync(true);
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync((DiscussionPost?)null);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Like(id));

        Assert.True(ReadBoolean(ok.Value, "ok"));
        Assert.Equal(0, ReadInt(ok.Value, "likes"));
        Assert.False(ReadBoolean(ok.Value, "likedByCurrentUser"));
    }

    [Fact]
    public async Task Like_WhenSuccessful_ReturnsUpdatedLikeState()
    {
        Guid id = Guid.NewGuid();
        var post = new DiscussionPost
        {
            Id = id,
            AuthorId = "owner",
            Title = "T",
            Content = "C",
            Likes = 4,
            LikesCollection = new List<DiscussionLike>
            {
                new() { PostId = id, UserId = "user-1" }
            }
        };
        var (controller, discussions, _) = CreateController("user-1");
        discussions.Setup(service => service.ToggleLikeAsync(id, "user-1")).ReturnsAsync(true);
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync(post);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Like(id));

        Assert.Equal(4, ReadInt(ok.Value, "likes"));
        Assert.True(ReadBoolean(ok.Value, "likedByCurrentUser"));
    }

    private static (
        DiscussionPostsController Controller,
        Mock<IDiscussionService> Discussions,
        Mock<UserManager<User>> Users) CreateController(
        string? userId,
        bool isAdmin = false)
    {
        var discussions = new Mock<IDiscussionService>();
        var users = CreateUserManager();
        users.Setup(manager => manager.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        var claims = new List<Claim>();
        if (userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var controller = new DiscussionPostsController(discussions.Object, users.Object)
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

    private static int ReadInt(object? value, string propertyName) =>
        (int)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? 0);
}
