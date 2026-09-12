using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Tests.Support;

internal static class ControllerTestSupport
{
    public static Mock<UserManager<User>> CreateUserManager(User? currentUser = null)
    {
        var manager = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            NullLogger<UserManager<User>>.Instance);

        manager.Setup(value => value.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(currentUser);
        return manager;
    }

    public static DefaultHttpContext CreateHttpContext(
        string? userId = "user-1",
        bool isAdmin = false,
        TestSession? session = null)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        if (session != null)
        {
            context.Features.Set<ISessionFeature>(new TestSessionFeature(session));
        }

        return context;
    }

    public static void Attach(ControllerBase controller, HttpContext context)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    public static object? Read(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value);

    public static string? ReadString(object? value, string propertyName) =>
        Read(value, propertyName) as string;

    public static bool ReadBoolean(object? value, string propertyName) =>
        Read(value, propertyName) is true;

    public static int ReadInt(object? value, string propertyName) =>
        Read(value, propertyName) is int result ? result : 0;

    public static decimal ReadDecimal(object? value, string propertyName) =>
        Read(value, propertyName) is decimal result ? result : 0m;
}

internal sealed class TestSessionFeature(ISession session) : ISessionFeature
{
    public ISession Session { get; set; } = session;
}

internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);

    public bool IsAvailable => true;
    public string Id => "unit-test-session";
    public IEnumerable<string> Keys => values.Keys;

    public void Clear() => values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => values.Remove(key);
    public void Set(string key, byte[] value) => values[key] = value;
    public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
}
