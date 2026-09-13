using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.API.Security;

public interface IAdminPermissionEvaluator
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionId,
        CancellationToken cancellationToken = default);
}

public sealed class AdminPermissionEvaluator : IAdminPermissionEvaluator
{
    private readonly string _connectionString;

    public AdminPermissionEvaluator(AppDbContext database)
    {
        _connectionString = database.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Access-control database connection is unavailable.");
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true ||
            !AdminPermissions.IsKnown(permissionId))
        {
            return false;
        }

        string? userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        PermissionDefinition definition = AdminPermissions.All
            .Single(permission => string.Equals(
                permission.Id,
                permissionId,
                StringComparison.Ordinal));

        var acceptedPermissions = new List<string> { permissionId };
        string managePermission = $"{definition.Resource}.{AdminPermissions.Operations.Manage}";
        if (AdminPermissions.IsKnown(managePermission))
        {
            acceptedPermissions.Add(managePermission);
        }

        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var database = new AccessControlDbContext(options);

        string[] activeSystemRoleNames = await (
            from membership in database.UserRoles.AsNoTracking()
            join profile in database.RoleSecurityProfiles.AsNoTracking()
                on membership.RoleId equals profile.RoleId
            join role in database.Roles.AsNoTracking()
                on membership.RoleId equals role.Id
            where membership.UserId == userId &&
                  !profile.IsDisabled &&
                  profile.IsSystem
            select role.Name ?? string.Empty)
            .ToArrayAsync(cancellationToken);

        if (activeSystemRoleNames.Any(roleName =>
                SystemRoleCatalog.Find(roleName)?.RequiredPermissions.Contains(permissionId) == true))
        {
            return true;
        }

        return await (
            from membership in database.UserRoles.AsNoTracking()
            join profile in database.RoleSecurityProfiles.AsNoTracking()
                on membership.RoleId equals profile.RoleId
            join grant in database.RolePermissions.AsNoTracking()
                on membership.RoleId equals grant.RoleId
            where membership.UserId == userId &&
                  !profile.IsDisabled &&
                  acceptedPermissions.Contains(grant.PermissionId)
            select grant.PermissionId)
            .AnyAsync(cancellationToken);
    }
}

public sealed record AdminPermissionRequirement(string PermissionId)
    : IAuthorizationRequirement;

public sealed class AdminPermissionAuthorizationHandler(
    IAdminPermissionEvaluator evaluator)
    : AuthorizationHandler<AdminPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPermissionRequirement requirement)
    {
        if (await evaluator.HasPermissionAsync(
                context.User,
                requirement.PermissionId))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Enforces the permission registry before the legacy Admin role attributes run.
/// A successful granular authorization receives an in-memory Admin role claim for
/// this request only, allowing the existing attributes to remain as a fallback
/// boundary while custom roles are authorized by permissions. The claim is never
/// persisted to the Identity cookie.
/// </summary>
public sealed class AdminPermissionEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuthorizationService authorizationService)
    {
        ControllerActionDescriptor? action = context.GetEndpoint()?
            .Metadata
            .GetMetadata<ControllerActionDescriptor>();

        AdminEndpointDefinition? definition = action is null
            ? null
            : ResolveDefinition(
                action.ControllerName,
                action.ActionName,
                context.Request.Method);

        if (definition is null || definition.MixedAccess)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string? permissionId = await ResolvePermissionAsync(
            definition,
            context.Request,
            context.RequestAborted);

        if (permissionId is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        AuthorizationResult result = await authorizationService.AuthorizeAsync(
            context.User,
            resource: null,
            requirements: [new AdminPermissionRequirement(permissionId)]);

        if (!result.Succeeded)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        AddRequestOnlyAdminRole(context.User);
        await next(context);
    }

    internal static AdminEndpointDefinition? ResolveDefinition(
        string controller,
        string action,
        string httpMethod)
    {
        return AdminEndpointRegistry.All.SingleOrDefault(endpoint =>
            string.Equals(endpoint.Controller, controller, StringComparison.Ordinal) &&
            string.Equals(NormalizeAction(endpoint.Action), action, StringComparison.Ordinal) &&
            string.Equals(endpoint.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<string?> ResolvePermissionAsync(
        AdminEndpointDefinition definition,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (definition.Permissions.Count == 1)
        {
            return definition.Permissions[0];
        }

        string? lifecycleAction = await ReadLifecycleActionAsync(
            request,
            cancellationToken);

        string? operation = lifecycleAction?.Trim().ToLowerInvariant() switch
        {
            "archive" => AdminPermissions.Operations.Archive,
            "delete" => AdminPermissions.Operations.Delete,
            "restore" => AdminPermissions.Operations.Restore,
            "unarchive" => AdminPermissions.Operations.Restore,
            _ => null
        };

        if (operation is null)
        {
            return null;
        }

        return definition.Permissions.SingleOrDefault(permission =>
            permission.EndsWith($".{operation}", StringComparison.Ordinal));
    }

    private static async Task<string?> ReadLifecycleActionAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        try
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            using JsonDocument document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        "action",
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }

    private static string NormalizeAction(string action)
    {
        int suffix = action.IndexOf('#');
        return suffix < 0 ? action : action[..suffix];
    }

    private static void AddRequestOnlyAdminRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(SystemRoleCatalog.Administrator))
        {
            return;
        }

        ClaimsIdentity? identity = user.Identities
            .FirstOrDefault(candidate => candidate.IsAuthenticated);
        identity?.AddClaim(new Claim(
            identity.RoleClaimType,
            SystemRoleCatalog.Administrator));
    }
}
