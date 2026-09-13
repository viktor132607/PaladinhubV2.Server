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
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
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
        public const string Delete = "talent_pages.delete";
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

    public static class Database
    {
        public const string Read = "database.read";
    }

    public static class Media
    {
        public const string Read = "media.read";
        public const string Create = "media.create";
        public const string Update = "media.update";
        public const string Archive = "media.archive";
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
        public const string Delete = "spell_icons.delete";
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

    public static class Products
    {
        public const string Read = "products.read";
        public const string Create = "products.create";
        public const string Update = "products.update";
        public const string Delete = "products.delete";
        public const string Manage = "products.manage";
    }

    public static class ProductReviews
    {
        public const string Read = "product_reviews.read";
        public const string Delete = "product_reviews.delete";
        public const string Manage = "product_reviews.manage";
    }

    public static class PromoCodes
    {
        public const string Read = "promo_codes.read";
        public const string Create = "promo_codes.create";
        public const string Update = "promo_codes.update";
        public const string Delete = "promo_codes.delete";
        public const string Manage = "promo_codes.manage";
    }

    private static readonly PermissionDefinition[] Definitions =
    [
        D(Users.Read, "users", Operations.Read, "View user administration data."),
        D(Users.Create, "users", Operations.Create, "Create users."),
        D(Users.Update, "users", Operations.Update, "Update users."),
        D(Users.Delete, "users", Operations.Delete, "Delete or disable users."),
        D(Users.Manage, "users", Operations.Manage, "Manage user security state."),
        D(Roles.Read, "roles", Operations.Read, "View roles."),
        D(Roles.Create, "roles", Operations.Create, "Create roles."),
        D(Roles.Update, "roles", Operations.Update, "Update roles."),
        D(Roles.Delete, "roles", Operations.Delete, "Delete roles."),
        D(Roles.Restore, "roles", Operations.Restore, "Restore role revisions."),
        D(Roles.Manage, "roles", Operations.Manage, "Manage protected role settings."),
        D(RolePermissions.Read, "role_permissions", Operations.Read, "View role permissions."),
        D(RolePermissions.Update, "role_permissions", Operations.Update, "Change role permissions."),
        D(RolePermissions.Manage, "role_permissions", Operations.Manage, "Manage role permission policy."),
        D(UserRoles.Read, "user_roles", Operations.Read, "View user-role assignments."),
        D(UserRoles.Update, "user_roles", Operations.Update, "Assign or revoke user roles."),
        D(UserRoles.Manage, "user_roles", Operations.Manage, "Manage user-role assignments."),
        D(Pages.Read, "pages", Operations.Read, "View Page Builder pages and history."),
        D(Pages.Create, "pages", Operations.Create, "Create Page Builder pages."),
        D(Pages.Update, "pages", Operations.Update, "Edit Page Builder pages and layouts."),
        D(Pages.Archive, "pages", Operations.Archive, "Archive Page Builder pages."),
        D(Pages.Delete, "pages", Operations.Delete, "Delete Page Builder pages."),
        D(Pages.Restore, "pages", Operations.Restore, "Restore Page Builder pages or revisions."),
        D(Pages.Manage, "pages", Operations.Manage, "Manage Page Builder lifecycle."),
        D(PageBlocks.Read, "page_blocks", Operations.Read, "Render Page Builder previews."),
        D(PageBlocks.Manage, "page_blocks", Operations.Manage, "Manage Page Builder block tooling."),
        D(PageTemplates.Read, "page_templates", Operations.Read, "View content templates."),
        D(PageTemplates.Create, "page_templates", Operations.Create, "Create content templates."),
        D(PageTemplates.Update, "page_templates", Operations.Update, "Update content templates."),
        D(PageTemplates.Archive, "page_templates", Operations.Archive, "Archive content templates."),
        D(PageTemplates.Delete, "page_templates", Operations.Delete, "Delete content templates."),
        D(PageTemplates.Restore, "page_templates", Operations.Restore, "Restore content template revisions."),
        D(PageTemplates.Manage, "page_templates", Operations.Manage, "Manage content template lifecycle."),
        D(PagePresets.Read, "page_presets", Operations.Read, "View and preview data presets."),
        D(PagePresets.Create, "page_presets", Operations.Create, "Create data presets."),
        D(PagePresets.Update, "page_presets", Operations.Update, "Update data presets."),
        D(PagePresets.Delete, "page_presets", Operations.Delete, "Delete data presets."),
        D(PagePresets.Manage, "page_presets", Operations.Manage, "Manage data presets."),
        D(TalentPages.Read, "talent_pages", Operations.Read, "View dynamic talent pages."),
        D(TalentPages.Create, "talent_pages", Operations.Create, "Create dynamic talent pages."),
        D(TalentPages.Update, "talent_pages", Operations.Update, "Update dynamic talent pages."),
        D(TalentPages.Delete, "talent_pages", Operations.Delete, "Delete dynamic talent pages."),
        D(TalentPages.Manage, "talent_pages", Operations.Manage, "Manage dynamic talent pages."),
        D(TalentTrees.Read, "talent_trees", Operations.Read, "View talent-tree administration data."),
        D(TalentTrees.Update, "talent_trees", Operations.Update, "Save talent-tree administration state."),
        D(TalentTrees.Manage, "talent_trees", Operations.Manage, "Manage talent-tree configuration."),
        D(Navigation.Read, "navigation", Operations.Read, "View navigation administration data."),
        D(Navigation.Create, "navigation", Operations.Create, "Create navigation links."),
        D(Navigation.Update, "navigation", Operations.Update, "Update navigation links."),
        D(Navigation.Delete, "navigation", Operations.Delete, "Delete navigation links."),
        D(Navigation.Restore, "navigation", Operations.Restore, "Restore navigation revisions."),
        D(Navigation.Manage, "navigation", Operations.Manage, "Manage navigation lifecycle."),
        D(Banners.Read, "banners", Operations.Read, "View banners and history."),
        D(Banners.Create, "banners", Operations.Create, "Create banners."),
        D(Banners.Update, "banners", Operations.Update, "Update banners."),
        D(Banners.Archive, "banners", Operations.Archive, "Archive or unarchive banners."),
        D(Banners.Delete, "banners", Operations.Delete, "Delete banners."),
        D(Banners.Restore, "banners", Operations.Restore, "Restore banner revisions."),
        D(Banners.Manage, "banners", Operations.Manage, "Manage banner lifecycle."),
        D(Footer.Read, "footer", Operations.Read, "View footer administration data."),
        D(Footer.Create, "footer", Operations.Create, "Create footer entries."),
        D(Footer.Update, "footer", Operations.Update, "Update footer entries."),
        D(Footer.Archive, "footer", Operations.Archive, "Archive or unarchive footer entries."),
        D(Footer.Delete, "footer", Operations.Delete, "Delete footer entries."),
        D(Footer.Restore, "footer", Operations.Restore, "Restore footer revisions."),
        D(Footer.Manage, "footer", Operations.Manage, "Manage footer lifecycle."),
        D(Seo.Read, "seo", Operations.Read, "View SEO targets, entries and history."),
        D(Seo.Create, "seo", Operations.Create, "Create SEO entries."),
        D(Seo.Update, "seo", Operations.Update, "Update SEO entries."),
        D(Seo.Archive, "seo", Operations.Archive, "Archive or unarchive SEO entries."),
        D(Seo.Delete, "seo", Operations.Delete, "Delete SEO entries."),
        D(Seo.Restore, "seo", Operations.Restore, "Restore SEO revisions."),
        D(Seo.Manage, "seo", Operations.Manage, "Manage SEO lifecycle."),
        D(Localization.Read, "localization", Operations.Read, "View language administration data."),
        D(Localization.Create, "localization", Operations.Create, "Create languages."),
        D(Localization.Update, "localization", Operations.Update, "Update translations."),
        D(Localization.Archive, "localization", Operations.Archive, "Archive or unarchive languages."),
        D(Localization.Delete, "localization", Operations.Delete, "Delete languages."),
        D(Localization.Restore, "localization", Operations.Restore, "Restore language revisions."),
        D(Localization.Manage, "localization", Operations.Manage, "Manage localization lifecycle."),
        D(Database.Read, "database", Operations.Read, "Browse administrative game data."),
        D(Media.Read, "media", Operations.Read, "View media and history."),
        D(Media.Create, "media", Operations.Create, "Upload media."),
        D(Media.Update, "media", Operations.Update, "Update media."),
        D(Media.Archive, "media", Operations.Archive, "Archive or unarchive media."),
        D(Media.Delete, "media", Operations.Delete, "Delete media."),
        D(Media.Restore, "media", Operations.Restore, "Restore media revisions."),
        D(Media.Manage, "media", Operations.Manage, "Manage media lifecycle."),
        D(Categories.Read, "categories", Operations.Read, "View categories and history."),
        D(Categories.Create, "categories", Operations.Create, "Create categories."),
        D(Categories.Update, "categories", Operations.Update, "Update categories."),
        D(Categories.Delete, "categories", Operations.Delete, "Delete categories."),
        D(Categories.Restore, "categories", Operations.Restore, "Restore category revisions."),
        D(Categories.Manage, "categories", Operations.Manage, "Manage categories."),
        D(Classes.Read, "classes", Operations.Read, "View classes and specializations."),
        D(Classes.Create, "classes", Operations.Create, "Create classes or specializations."),
        D(Classes.Update, "classes", Operations.Update, "Update classes or specializations."),
        D(Classes.Delete, "classes", Operations.Delete, "Delete classes or specializations."),
        D(Classes.Restore, "classes", Operations.Restore, "Restore class revisions."),
        D(Classes.Manage, "classes", Operations.Manage, "Manage classes and specializations."),
        D(Tags.Read, "tags", Operations.Read, "View tags and history."),
        D(Tags.Create, "tags", Operations.Create, "Create tags."),
        D(Tags.Update, "tags", Operations.Update, "Update tags."),
        D(Tags.Delete, "tags", Operations.Delete, "Delete tags."),
        D(Tags.Restore, "tags", Operations.Restore, "Restore tag revisions."),
        D(Tags.Manage, "tags", Operations.Manage, "Manage tags."),
        D(Patches.Read, "patches", Operations.Read, "View patches and history."),
        D(Patches.Create, "patches", Operations.Create, "Create patches."),
        D(Patches.Update, "patches", Operations.Update, "Update patches."),
        D(Patches.Delete, "patches", Operations.Delete, "Delete patches."),
        D(Patches.Restore, "patches", Operations.Restore, "Restore patch revisions."),
        D(Patches.Manage, "patches", Operations.Manage, "Manage patches."),
        D(Rarities.Read, "rarities", Operations.Read, "View rarities and history."),
        D(Rarities.Create, "rarities", Operations.Create, "Create rarities."),
        D(Rarities.Update, "rarities", Operations.Update, "Update rarities."),
        D(Rarities.Delete, "rarities", Operations.Delete, "Delete rarities."),
        D(Rarities.Restore, "rarities", Operations.Restore, "Restore rarity revisions."),
        D(Rarities.Manage, "rarities", Operations.Manage, "Manage rarities."),
        D(RecordTypes.Read, "record_types", Operations.Read, "View record types."),
        D(RecordTypes.Create, "record_types", Operations.Create, "Create record types."),
        D(RecordTypes.Update, "record_types", Operations.Update, "Rename record types."),
        D(RecordTypes.Delete, "record_types", Operations.Delete, "Delete or replace record types."),
        D(RecordTypes.Manage, "record_types", Operations.Manage, "Manage record types."),
        D(SpellIcons.Read, "spell_icons", Operations.Read, "Browse spell icons."),
        D(SpellIcons.Create, "spell_icons", Operations.Create, "Upload spell icons."),
        D(SpellIcons.Delete, "spell_icons", Operations.Delete, "Delete spell icons."),
        D(SpellIcons.Manage, "spell_icons", Operations.Manage, "Manage spell icons."),
        D(Spells.Read, "spells", Operations.Read, "View spell administration data."),
        D(Spells.Create, "spells", Operations.Create, "Create spells."),
        D(Spells.Update, "spells", Operations.Update, "Update spells."),
        D(Spells.Delete, "spells", Operations.Delete, "Delete spells."),
        D(Spells.Manage, "spells", Operations.Manage, "Manage spells."),
        D(Items.Read, "items", Operations.Read, "View item administration data."),
        D(Items.Create, "items", Operations.Create, "Create items."),
        D(Items.Update, "items", Operations.Update, "Update items."),
        D(Items.Delete, "items", Operations.Delete, "Delete items."),
        D(Items.Manage, "items", Operations.Manage, "Manage items."),
        D(Products.Read, "products", Operations.Read, "View product administration data and unpublished product details."),
        D(Products.Create, "products", Operations.Create, "Create products."),
        D(Products.Update, "products", Operations.Update, "Update products."),
        D(Products.Delete, "products", Operations.Delete, "Delete products."),
        D(Products.Manage, "products", Operations.Manage, "Manage store products."),
        D(ProductReviews.Read, "product_reviews", Operations.Read, "View product review administration data."),
        D(ProductReviews.Delete, "product_reviews", Operations.Delete, "Moderate product reviews."),
        D(ProductReviews.Manage, "product_reviews", Operations.Manage, "Manage product review moderation."),
        D(PromoCodes.Read, "promo_codes", Operations.Read, "View promo codes."),
        D(PromoCodes.Create, "promo_codes", Operations.Create, "Create promo codes."),
        D(PromoCodes.Update, "promo_codes", Operations.Update, "Deactivate or update promo codes."),
        D(PromoCodes.Delete, "promo_codes", Operations.Delete, "Delete promo codes."),
        D(PromoCodes.Manage, "promo_codes", Operations.Manage, "Manage promo codes.")
    ];

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

    private static PermissionDefinition D(
        string id,
        string resource,
        string operation,
        string description)
    {
        return new PermissionDefinition(id, resource, operation, description);
    }
}
