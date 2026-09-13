using PaladinHubV2.Server.API.Security;
using PaladinHubV2.Server.Core.Security;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlPermissionPurposeTests
{
    [Fact]
    public void EveryOperationalPermissionHasAnEndpointOrExplicitClientPurpose()
    {
        HashSet<string> used = AdminEndpointRegistry.All
            .SelectMany(endpoint => endpoint.Permissions)
            .ToHashSet(StringComparer.Ordinal);

        // The talent-tree reader controls access to the builder UI. Its data
        // source is intentionally public because the public guide also reads it.
        used.Add(AdminPermissions.TalentTrees.Read);

        string[] unused = AdminPermissions.All
            .Where(permission => permission.Operation != AdminPermissions.Operations.Manage)
            .Select(permission => permission.Id)
            .Where(permission => !used.Contains(permission))
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unused.Length == 0,
            "Operational permissions without a concrete purpose: " +
            string.Join(", ", unused));
    }
}
