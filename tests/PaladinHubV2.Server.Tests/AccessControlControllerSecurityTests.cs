using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PaladinHubV2.Server.API.Controllers.Admin;
using PaladinHubV2.Server.API.Security;
using PaladinHubV2.Server.Core.Security;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlControllerSecurityTests
{
    [Fact]
    public void ControllerKeepsLegacyAdminBoundaryAndAutomaticAntiforgery()
    {
        Type controller = typeof(AccessControlController);
        AuthorizeAttribute authorize = Assert.Single(controller
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", authorize.Roles);
        Assert.Single(controller
            .GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), inherit: true));
    }

    [Fact]
    public void EveryAccessControlActionIsRegisteredWithKnownPermissions()
    {
        string[] actions = typeof(AccessControlController)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Any())
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (string action in actions)
        {
            Assert.True(AdminEndpointRegistry.TryGet("AccessControl", action, out IReadOnlyList<AdminEndpointDefinition>? mappings),
                $"AccessControl.{action} is missing from the administrative endpoint registry.");
            Assert.NotEmpty(mappings);
            Assert.All(mappings, mapping =>
            {
                Assert.NotEmpty(mapping.Permissions);
                Assert.All(mapping.Permissions, permission => Assert.True(AdminPermissions.IsKnown(permission)));
            });
        }
    }
}
