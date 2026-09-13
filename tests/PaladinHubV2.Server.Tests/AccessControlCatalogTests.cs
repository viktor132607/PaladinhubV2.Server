using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.API.Security;
using PaladinHubV2.Server.Core.Security;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlCatalogTests
{
    [Fact]
    public void PermissionCatalogUsesUniqueStableIds()
    {
        Assert.NotEmpty(AdminPermissions.All);
        Assert.Equal(
            AdminPermissions.All.Count,
            AdminPermissions.All.Select(permission => permission.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (PermissionDefinition permission in AdminPermissions.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Resource));
            Assert.False(string.IsNullOrWhiteSpace(permission.Operation));
            Assert.Matches("^[a-z0-9_]+\\.[a-z0-9_]+$", permission.Id);
            Assert.True(permission.Id.Length <= 128);
            Assert.True(AdminPermissions.IsKnown(permission.Id));
        }
    }

    [Fact]
    public void PermissionValidatorRejectsUnknownPermissionsAndDeduplicatesKnownOnes()
    {
        Assert.Throws<ArgumentException>(() =>
            PermissionCatalogValidator.NormalizeAndValidate(["made_up.manage"]));

        IReadOnlyList<string> normalized =
            PermissionCatalogValidator.NormalizeAndValidate([
                AdminPermissions.Pages.Read,
                AdminPermissions.Pages.Update,
                AdminPermissions.Pages.Read
            ]);

        Assert.Equal(
            [AdminPermissions.Pages.Read, AdminPermissions.Pages.Update],
            normalized);
    }

    [Fact]
    public void ProtectedAdminRoleMapsToTheEntireCurrentPermissionCatalog()
    {
        SystemRoleDefinition admin = SystemRoleCatalog.Find("admin")!;

        Assert.NotNull(admin);
        Assert.Equal(SystemRoleCatalog.Administrator, admin.Name);
        Assert.True(admin.ProtectName);
        Assert.True(admin.ProtectDeletion);
        Assert.True(admin.ProtectPermissionSet);
        Assert.Equal(
            AdminPermissions.AllIds.Order(StringComparer.Ordinal),
            admin.RequiredPermissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EndpointRegistryContainsOnlyKnownPermissionsAndUniqueEndpointRows()
    {
        Assert.NotEmpty(AdminEndpointRegistry.All);

        var duplicateRows = AdminEndpointRegistry.All
            .GroupBy(
                endpoint => new
                {
                    endpoint.Controller,
                    Action = NormalizeAction(endpoint.Action),
                    endpoint.HttpMethod,
                    endpoint.Route
                })
            .Where(group => group.Count() > 1)
            .ToArray();

        Assert.Empty(duplicateRows);

        foreach (AdminEndpointDefinition endpoint in AdminEndpointRegistry.All)
        {
            Assert.NotEmpty(endpoint.Permissions);
            foreach (string permission in endpoint.Permissions)
            {
                Assert.True(
                    AdminPermissions.IsKnown(permission),
                    $"{endpoint.Controller}.{endpoint.Action} maps unknown permission '{permission}'.");
            }
        }
    }

    [Fact]
    public void EveryLegacyAdminAuthorizedControllerActionIsInventoried()
    {
        Assembly apiAssembly = typeof(SeoController).Assembly;
        var missing = new List<string>();

        IEnumerable<Type> controllerTypes = apiAssembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal));

        foreach (Type controllerType in controllerTypes)
        {
            string controller = controllerType.Name[..^"Controller".Length];
            AuthorizeAttribute[] controllerAuthorize = controllerType
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .ToArray();

            foreach (MethodInfo method in controllerType.GetMethods(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.DeclaredOnly))
            {
                HttpMethodAttribute[] verbs = method
                    .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                    .ToArray();
                if (verbs.Length == 0 ||
                    method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                {
                    continue;
                }

                bool adminAuthorized = controllerAuthorize
                    .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                    .Any(attribute => HasAdminRole(attribute.Roles));
                if (!adminAuthorized)
                {
                    continue;
                }

                foreach (string httpMethod in verbs.SelectMany(verb => verb.HttpMethods).Distinct())
                {
                    bool found = AdminEndpointRegistry.All.Any(endpoint =>
                        string.Equals(endpoint.Controller, controller, StringComparison.Ordinal) &&
                        string.Equals(NormalizeAction(endpoint.Action), method.Name, StringComparison.Ordinal) &&
                        string.Equals(endpoint.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase));

                    if (!found)
                    {
                        missing.Add($"{controller}.{method.Name} [{httpMethod}]");
                    }
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "Legacy Admin-authorized actions missing from AdminEndpointRegistry: " +
            string.Join(", ", missing.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void RegistryExplicitlyTracksNonPrefixAndMixedAdminCapabilities()
    {
        Assert.Contains(
            AdminEndpointRegistry.All,
            endpoint => endpoint.Controller == "Presets" && endpoint.Route.StartsWith("/api/", StringComparison.Ordinal));
        Assert.Contains(
            AdminEndpointRegistry.All,
            endpoint => endpoint.Controller == "TalentsApi" && endpoint.Route.StartsWith("/api/", StringComparison.Ordinal));
        Assert.Contains(
            AdminEndpointRegistry.All,
            endpoint => endpoint.Controller == "PageBlocks" && endpoint.CurrentProtection.Contains("public", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            AdminEndpointRegistry.All,
            endpoint => endpoint.MixedAccess && endpoint.Controller == "ProductReviews");
        Assert.Contains(
            AdminEndpointRegistry.All,
            endpoint => endpoint.MixedAccess && endpoint.Controller == "Products");
    }

    private static bool HasAdminRole(string? roles)
    {
        if (string.IsNullOrWhiteSpace(roles))
        {
            return false;
        }

        return roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(role => string.Equals(role, SystemRoleCatalog.Administrator, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAction(string action)
    {
        int suffix = action.IndexOf('#');
        return suffix < 0 ? action : action[..suffix];
    }
}
