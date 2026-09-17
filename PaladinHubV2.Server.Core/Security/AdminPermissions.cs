using System.Reflection;

namespace PaladinHubV2.Server.Core.Security;

public sealed record PermissionDefinition(
    string Id,
    string Resource,
    string Operation,
    string Description);

public static class AdminPermissions
{
    public static class Operations
    {
        public const string Read = "read";
        public const string Create = "create";
        public const string Update = "update";
        public const string Archive = "archive";
        public const string Delete = "delete";
        public const string Restore = "restore";
        public const string Manage = "manage";
    }

    public static class Users
    {
        public const string Read = "users.read";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Create = "roles.create";
        public const string Update = "roles.update";
        public const string Delete = "roles.delete";
        public const string Restore = "roles.restore";
        public const string Manage = "roles.manage";
    }

    public static class RolePermissions
    {
        public const string Read = "role_permissions.read";
        public const string Update = "role_permissions.update";
        public const string Manage = "role_permissions.manage";
    }

    public static class UserRoles
    {
        public const string Read = "user_roles.read";
        public const string Update = "user_roles.update";
        public const string Manage = "user_roles.manage";
    }

    public static class Pages
    {
        public const string Read = "pages.read";
        public const string Create = "pages.create";
        public const string Update = "pages.update";
        public const string Archive = "pages.archive";
        public const string Delete = "pages.delete";
        public const string Restore = "pages.restore";
        public const string Manage = "pages.manage";
    }

    public static class PageBlocks
    {
        public const string Read = "page_blocks.read";
        public const string Manage = "page_blocks.manage";
    }

    public static class PageTemplates
    {
        public const string Read = "page_templates.read";
        public const string Create = "page_templates.create";
        public const string Update = "page_templates.update";
        public const string Archive = "page_templates.archive";
        public const string Delete = "page_templates.delete";
        public const string Restore = "page_templates.restore";
        public const string Manage = "page_templates.manage";
    }

    public static class PagePresets
    {
        public const string Read = "page_presets.read";
        public const string Create = "page_presets.create";
        public const string Update = "page_presets.update";
        public const string Delete = "page_presets.delete";
        public const string Manage = "page_presets.manage";
    }

    public static class TalentPages
    {
        public const string Read = "talent_pages.read";
        public const string Create = "talent_pages.create";
        public const string Update = "talent_pages.update";
        public const string Manage = "talent_pages.manage";
    }

    public static class TalentTrees
    {
        public const string Read = "talent_trees.read";
        public const string Update = "talent_trees.update";
        public const string Manage = "talent_trees.manage";
    }

    public static class Navigation
    {
        public const string Read = "navigation.read";
        public const string Create = "navigation.create";
        public const string Update = "navigation.update";
        public const string Delete = "navigation.delete";
        public const string Restore = "navigation.restore";
        public const string Manage = "navigation.manage";
    }

    public static class Banners
    {
        public const string Read = "banners.read";
        public const string Create = "banners.create";
        public const string Update = "banners.update";
        public const string Archive = "banners.archive";
        public const string Delete = "banners.delete";
        public const string Restore = "banners.restore";
        public const string Manage = "banners.manage";
    }

    public static class Footer
    {
        public const string Read = "footer.read";
        public const string Create = "footer.create";
        public const string Update = "footer.update";
        public const string Archive = "footer.archive";
        public const string Delete = "footer.delete";
        public const string Restore = "footer.restore";
        public const string Manage = "footer.manage";
    }

    public static class Seo
    {
        public const string Read = "seo.read";
        public const string Create = "seo.create";
        public const string Update = "seo.update";
        public const string Archive = "seo.archive";
        public const string Delete = "seo.delete";
        public const string Restore = "seo.restore";
        public const string Manage = "seo.manage";
    }

    public static class Localization
    {
        public const string Read = "localization.read";
        public const string Create = "localization.create";
        public const string Update = "localization.update";
        public const string Archive = "localization.archive";
        public const string Delete = "localization.delete";
        public const string Restore = "localization.restore";
        public const string Manage = "localization.manage";
    }

    public static class DatabaseBackups
    {
        public const string Read = "database_backups.read";
        public const string Restore = "database_backups.restore";
    }

    public static class Database
    {
        public const string Read = "database.read";
    }

    public static class Media
    {
        public const string Read = "media.read";
        public const string Update = "media.update";
        public const string Delete = "media.delete";
        public const string Restore = "media.restore";
        public const string Manage = "media.manage";
    }

    public static class Categories
    {
        public const string Read = "categories.read";
        public const string Create = "categories.create";
        public const string Update = "categories.update";
        public const string Delete = "categories.delete";
        public const string Restore = "categories.restore";
        public const string Manage = "categories.manage";
    }

    public static class Classes
    {
        public const string Read = "classes.read";
        public const string Create = "classes.create";
        public const string Update = "classes.update";
        public const string Delete = "classes.delete";
        public const string Restore = "classes.restore";
        public const string Manage = "classes.manage";
    }

    public static class Tags
    {
        public const string Read = "tags.read";
        public const string Create = "tags.create";
        public const string Update = "tags.update";
        public const string Delete = "tags.delete";
        public const string Restore = "tags.restore";
        public const string Manage = "tags.manage";
    }

    public static class Patches
    {
        public const string Read = "patches.read";
        public const string Create = "patches.create";
        public const string Update = "patches.update";
        public const string Delete = "patches.delete";
        public const string Restore = "patches.restore";
        public const string Manage = "patches.manage";
    }

    public static class Rarities
    {
        public const string Read = "rarities.read";
        public const string Create = "rarities.create";
        public const string Update = "rarities.update";
        public const string Delete = "rarities.delete";
        public const string Restore = "rarities.restore";
        public const string Manage = "rarities.manage";
    }

    public static class RecordTypes
    {
        public const string Read = "record_types.read";
        public const string Create = "record_types.create";
        public const string Update = "record_types.update";
        public const string Delete = "record_types.delete";
        public const string Manage = "record_types.manage";
    }

    public static class SpellIcons
    {
        public const string Read = "spell_icons.read";
        public const string Create = "spell_icons.create";
        public const string Manage = "spell_icons.manage";
    }

    public static class Spells
    {
        public const string Read = "spells.read";
        public const string Create = "spells.create";
        public const string Update = "spells.update";
        public const string Delete = "spells.delete";
        public const string Manage = "spells.manage";
    }

    public static class Items
    {
        public const string Read = "items.read";
        public const string Create = "items.create";
        public const string Update = "items.update";
        public const string Delete = "items.delete";
        public const string Manage = "items.manage";
    }

    public static class Carts
    {
        public const string Read = "carts.read";
        public const string Manage = "carts.manage";
    }

    public static class Products
    {
        public const string Read = "products.read";
        public const string Create = "products.create";
        public const string Update = "products.update";
        public const string Delete = "products.delete";
        public const string Manage = "products.manage";
    }

    public static class DiscussionPosts
    {
        public const string Delete = "discussion_posts.delete";
        public const string Manage = "discussion_posts.manage";
    }

    public static class ProductReviews
    {
        public const string Delete = "product_reviews.delete";
        public const string Manage = "product_reviews.manage";
    }

    public static class PromoCodes
    {
        public const string Read = "promo_codes.read";
        public const string Create = "promo_codes.create";
        public const string Update = "promo_codes.update";
        public const string Manage = "promo_codes.manage";
    }

    private static readonly PermissionDefinition[] Definitions = BuildDefinitions();

    public static IReadOnlyList<PermissionDefinition> All => Definitions;

    public static IReadOnlySet<string> AllIds { get; } =
        Definitions.Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string? permissionId)
    {
        return !string.IsNullOrWhiteSpace(permissionId) &&
               AllIds.Contains(permissionId.Trim());
    }

    public static IReadOnlyList<PermissionDefinition> ForResource(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        return Definitions
            .Where(definition => string.Equals(
                definition.Resource,
                resource.Trim(),
                StringComparison.Ordinal))
            .ToArray();
    }

    private static PermissionDefinition[] BuildDefinitions()
    {
        return typeof(AdminPermissions)
            .GetNestedTypes(BindingFlags.Public)
            .Where(type => type != typeof(Operations))
            .SelectMany(type => type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field =>
                    field.IsLiteral &&
                    !field.IsInitOnly &&
                    field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!))
            .Distinct(StringComparer.Ordinal)
            .Select(permissionId =>
            {
                string[] parts = permissionId.Split('.', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException(
                        $"Invalid permission constant '{permissionId}'. Expected resource.operation.");
                }

                string resource = parts[0];
                string operation = parts[1];
                return new PermissionDefinition(
                    permissionId,
                    resource,
                    operation,
                    $"{operation} permission for {resource.Replace('_', ' ')}.");
            })
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
