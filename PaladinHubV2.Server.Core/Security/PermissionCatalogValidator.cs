namespace PaladinHubV2.Server.Core.Security;

public static class PermissionCatalogValidator
{
    public static IReadOnlyList<string> NormalizeAndValidate(
        IEnumerable<string>? permissionIds)
    {
        if (permissionIds is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string? raw in permissionIds)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new ArgumentException("Permission IDs cannot be empty.", nameof(permissionIds));
            }

            string permissionId = raw.Trim();
            if (!AdminPermissions.IsKnown(permissionId))
            {
                throw new ArgumentException(
                    $"Unknown permission '{permissionId}'.",
                    nameof(permissionIds));
            }

            if (seen.Add(permissionId))
            {
                normalized.Add(permissionId);
            }
        }

        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }

    public static void EnsureKnown(string permissionId)
    {
        if (!AdminPermissions.IsKnown(permissionId))
        {
            throw new ArgumentException(
                $"Unknown permission '{permissionId}'.",
                nameof(permissionId));
        }
    }
}
