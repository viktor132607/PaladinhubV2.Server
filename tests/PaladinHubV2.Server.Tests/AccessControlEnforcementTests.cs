using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using PaladinHubV2.Server.API.Controllers.Content.PageBuilder;
using PaladinHubV2.Server.API.Controllers.Content.Talents;
using PaladinHubV2.Server.API.Security;
using PaladinHubV2.Server.Core.Security;
using Xunit;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlEnforcementTests
{
    [Fact]
    public async Task AnonymousMappedAdminEndpointIsRejectedBeforeController()
    {
        bool nextCalled = false;
        var middleware = new AdminPermissionEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var authorization = new RecordingAuthorizationService(succeed: true);
        DefaultHttpContext context = Context(
            controller: "PageBlocks",
            action: "Render",
            method: "POST",
            authenticated: false);

        await middleware.InvokeAsync(context, authorization);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal(0, authorization.CallCount);
    }

    [Fact]
    public async Task MissingGranularPermissionReturnsForbidden()
    {
        bool nextCalled = false;
        var middleware = new AdminPermissionEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var authorization = new RecordingAuthorizationService(succeed: false);
        DefaultHttpContext context = Context(
            controller: "Presets",
            action: "Create",
            method: "POST",
            authenticated: true);

        await middleware.InvokeAsync(context, authorization);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal(AdminPermissions.PagePresets.Create, authorization.LastPermission);
    }

    [Fact]
    public async Task GrantedGranularPermissionAllowsLegacyAdminAttributeForThisRequestOnly()
    {
        bool nextCalled = false;
        var middleware = new AdminPermissionEnforcementMiddleware(context =>
        {
            nextCalled = true;
            Assert.True(context.User.IsInRole(SystemRoleCatalog.Administrator));
            return Task.CompletedTask;
        });
        var authorization = new RecordingAuthorizationService(succeed: true);
        DefaultHttpContext context = Context(
            controller: "Presets",
            action: "Create",
            method: "POST",
            authenticated: true);

        Assert.False(context.User.IsInRole(SystemRoleCatalog.Administrator));

        await middleware.InvokeAsync(context, authorization);

        Assert.True(nextCalled);
        Assert.Equal(AdminPermissions.PagePresets.Create, authorization.LastPermission);
    }

    [Fact]
    public async Task LifecyclePermissionIsResolvedFromBodyAndBodyIsRewound()
    {
        var middleware = new AdminPermissionEnforcementMiddleware(_ => Task.CompletedTask);
        var authorization = new RecordingAuthorizationService(succeed: true);
        DefaultHttpContext context = Context(
            controller: "Banners",
            action: "Change",
            method: "POST",
            authenticated: true);
        byte[] payload = Encoding.UTF8.GetBytes("{\"version\":2,\"action\":\"delete\"}");
        context.Request.Body = new MemoryStream(payload);
        context.Request.ContentLength = payload.Length;
        context.Request.ContentType = "application/json";

        await middleware.InvokeAsync(context, authorization);

        Assert.Equal(AdminPermissions.Banners.Delete, authorization.LastPermission);
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Fact]
    public async Task MixedAccessEndpointIsNotBlanketLockedByMiddleware()
    {
        bool nextCalled = false;
        var middleware = new AdminPermissionEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var authorization = new RecordingAuthorizationService(succeed: false);
        DefaultHttpContext context = Context(
            controller: "Products",
            action: "DetailsApi",
            method: "GET",
            authenticated: false);

        await middleware.InvokeAsync(context, authorization);

        Assert.True(nextCalled);
        Assert.Equal(0, authorization.CallCount);
    }

    [Fact]
    public void PresetAndTalentMutationsUseAutomaticAntiforgeryValidation()
    {
        Assert.NotNull(
            typeof(PresetsController)
                .GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), inherit: true)
                .SingleOrDefault());
        Assert.NotNull(
            typeof(TalentsApiController)
                .GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), inherit: true)
                .SingleOrDefault());
    }

    [Fact]
    public async Task HandlerUsesPermissionEvaluatorInsteadOfRoleClaim()
    {
        var evaluator = new StubPermissionEvaluator(allowed: true);
        var handler = new AdminPermissionAuthorizationHandler(evaluator);
        var requirement = new AdminPermissionRequirement(AdminPermissions.Products.Update);
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "editor-1")],
                "test"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal(AdminPermissions.Products.Update, evaluator.LastPermission);
    }

    private static DefaultHttpContext Context(
        string controller,
        string action,
        string method,
        bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.User = authenticated
            ? new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim(ClaimTypes.Name, "Editor")
                    ],
                    "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = controller,
            ActionName = action
        };
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(descriptor),
            $"{controller}.{action}"));
        return context;
    }

    private sealed class RecordingAuthorizationService(bool succeed)
        : IAuthorizationService
    {
        public int CallCount { get; private set; }
        public string? LastPermission { get; private set; }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            CallCount++;
            LastPermission = requirements
                .OfType<AdminPermissionRequirement>()
                .SingleOrDefault()?
                .PermissionId;
            return Task.FromResult(
                succeed
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPermissionEvaluator(bool allowed)
        : IAdminPermissionEvaluator
    {
        public string? LastPermission { get; private set; }

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionId,
            CancellationToken cancellationToken = default)
        {
            LastPermission = permissionId;
            return Task.FromResult(allowed);
        }
    }
}
