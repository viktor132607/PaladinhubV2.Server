namespace PaladinHubV2.Server.Core.Security;

public sealed record SystemRoleDefinition(
    string Name,
    bool ProtectName,
    bool ProtectDeletion,
    bool ProtectPermissionSet,
    IReadOnlySet<string> RequiredPermissions);

public static class SystemRoleCatalog
{
    public const string Administrator = "Admin";

    private static readonly SystemRoleDefinition[] Definitions =
    [
        new(
            Administrator,
            ProtectName: true,
            ProtectDeletion: true,
            ProtectPermissionSet: true,
            RequiredPermissions: AdminPermissions.AllIds)
    ];

    public static IReadOnlyList<SystemRoleDefinition> All => Definitions;

    public static bool IsSystemRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        return Definitions.Any(role => string.Equals(
            role.Name,
            roleName.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }

    public static SystemRoleDefinition? Find(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        return Definitions.FirstOrDefault(role => string.Equals(
            role.Name,
            roleName.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }
}
