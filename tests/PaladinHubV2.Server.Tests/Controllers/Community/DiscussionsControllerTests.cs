using System.Collections;
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

public sealed class DiscussionsControllerTests
{
    [Fact]
    public async Task Index_WhenAnonymous_ProjectsFallbackAuthorCountsAndNoDeletePermission()
    {
        var post = new DiscussionPost
        {
            AuthorId = "owner-1",
            Author = null!,
            Title = "Title",
            Content = "Content",
            Likes = 3,
            Comments = new List<DiscussionComment> { new(), new() }
        };
        var (controller, discussions) = CreateController(null);
        discussions.Setup(service => service.GetAllAsync()).ReturnsAsync(new[] { post });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Index());
        object item = Materialize(ok.Value).Single();

        Assert.Equal("Unknown user", ReadString(item, "AuthorName"));
        Assert.Equal(2, ReadInt(item, "CommentsCount"));
        Assert.Equal(3, ReadInt(item, "Likes"));
        Assert.False(ReadBoolean(item, "CanDelete"));
    }

    [Fact]
    public async Task Index_WhenCurrentUserOwnsPost_AllowsDelete()
    {
        var post = Post("user-1");
        var (controller, discussions) = CreateController("user-1");
        discussions.Setup(service => service.GetAllAsync()).ReturnsAsync(new[] { post });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Index());
        object item = Materialize(ok.Value).Single();

        Assert.True(ReadBoolean(item, "CanDelete"));
    }

    [Fact]
    public async Task Index_WhenAdminDoesNotOwnPost_AllowsDelete()
    {
        var post = Post("other-user");
        var (controller, discussions) = CreateController("admin-1", isAdmin: true);
        discussions.Setup(service => service.GetAllAsync()).ReturnsAsync(new[] { post });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Index());
        object item = Materialize(ok.Value).Single();

        Assert.True(ReadBoolean(item, "CanDelete"));
    }

    [Fact]
    public async Task Details_WhenPostDoesNotExist_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        var (controller, discussions) = CreateController("user-1");
        discussions.Setup(service => service.GetByIdAsync(id)).ReturnsAsync((DiscussionPost?)null);

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(await controller.Details(id));

        Assert.Equal("Discussion not found.", ReadString(notFound.Value, "message"));
    }

    [Fact]
    public async Task Details_WhenAuthenticated_ProjectsLikesDeletePermissionAndNewestCommentsFirst()
    {
        DateTime older = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime newer = older.AddHours(1);
        var post = Post("user-1");
        post.LikesCollection.Add(new DiscussionLike { UserId = "user-1" });
        post.Comments = new List<DiscussionComment>
        {
            new()
            {
                AuthorId = "commenter-1",
                Author = new User { UserName = "Older" },
                Content = "Old",
                CreatedOn = older,
                Likes = 1
            },
            new()
            {
                AuthorId = "commenter-2",
                Author = null!,
                Content = "New",
                CreatedOn = newer,
                Likes = 2,
                LikesCollection = new List<DiscussionCommentLike>
                {
                    new() { UserId = "user-1" }
                }
            }
        };

        var (controller, discussions) = CreateController("user-1");
        discussions.Setup(service => service.GetByIdAsync(post.Id)).ReturnsAsync(post);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Details(post.Id));

        Assert.True(ReadBoolean(ok.Value!, "LikedByCurrentUser"));
        Assert.True(ReadBoolean(ok.Value!, "CanDelete"));
        List<object> comments = Materialize(ReadProperty(ok.Value!, "Comments"));
        Assert.Equal(2, comments.Count);
        Assert.Equal("New", ReadString(comments[0], "Content"));
        Assert.Equal("Unknown user", ReadString(comments[0], "AuthorName"));
        Assert.True(ReadBoolean(comments[0], "LikedByCurrentUser"));
        Assert.Equal("Old", ReadString(comments[1], "Content"));
        Assert.False(ReadBoolean(comments[1], "LikedByCurrentUser"));
    }

    [Fact]
    public async Task Details_WhenAnonymous_DoesNotMarkLikesOrDeletePermission()
    {
        var post = Post("owner-1");
        post.LikesCollection.Add(new DiscussionLike { UserId = "someone" });
        var (controller, discussions) = CreateController(null);
        discussions.Setup(service => service.GetByIdAsync(post.Id)).ReturnsAsync(post);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(await controller.Details(post.Id));

        Assert.False(ReadBoolean(ok.Value!, "LikedByCurrentUser"));
        Assert.False(ReadBoolean(ok.Value!, "CanDelete"));
    }

    private static DiscussionPost Post(string authorId) => new()
    {
        Id = Guid.NewGuid(),
        AuthorId = authorId,
        Author = new User { UserName = "Author" },
        Title = "Title",
        Content = "Content",
        Comments = new List<DiscussionComment>(),
        LikesCollection = new List<DiscussionLike>()
    };

    private static (DiscussionsController Controller, Mock<IDiscussionService> Discussions) CreateController(
        string? userId,
        bool isAdmin = false)
    {
        var discussions = new Mock<IDiscussionService>();
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        var claims = new List<Claim>();
        if (userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var controller = new DiscussionsController(discussions.Object, userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId == null ? null : "UnitTest"))
                }
            }
        };

        return (controller, discussions);
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

    private static List<object> Materialize(object? value) =>
        ((IEnumerable)(value ?? Array.Empty<object>())).Cast<object>().ToList();

    private static object? ReadProperty(object value, string propertyName) =>
        value.GetType().GetProperty(propertyName)?.GetValue(value);

    private static bool ReadBoolean(object value, string propertyName) =>
        ReadProperty(value, propertyName) is true;

    private static int ReadInt(object value, string propertyName) =>
        (int)(ReadProperty(value, propertyName) ?? 0);

    private static string? ReadString(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value) as string;
}
