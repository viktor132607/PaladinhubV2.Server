using PaladinHubV2.Server.Core.Security;

namespace PaladinHubV2.Server.API.Security;

public sealed record AdminEndpointDefinition(
    string Controller,
    string Action,
    string HttpMethod,
    string Route,
    string Resource,
    string Operation,
    string CurrentProtection,
    IReadOnlyList<string> Permissions,
    bool MixedAccess = false,
    string? Notes = null)
{
    public string Key => $"{Controller}.{Action}";
}

/// <summary>
/// Explicit inventory of the current administrative action surface. 15.1 records
/// what exists and which permission(s) each action must use. 15.3 replaces the
/// legacy Admin-role boundary with permission enforcement and adds reflection
/// coverage that refuses unmapped new administrative actions.
/// </summary>
public static class AdminEndpointRegistry
{
    private const string AdminRole = "Authorize(Admin)";
    private const string Authenticated = "Authorize + in-action Admin override";
    private const string PublicAdminHelper = "Currently public admin helper";

    private static readonly AdminEndpointDefinition[] Definitions =
    [
        E("Banners", "List", "GET", "/Admin/api/banners", "banners", "read", AdminRole, AdminPermissions.Banners.Read),
        E("Banners", "History", "GET", "/Admin/api/banners/{id}/history", "banners", "read", AdminRole, AdminPermissions.Banners.Read),
        E("Banners", "Create", "POST", "/Admin/api/banners", "banners", "create", AdminRole, AdminPermissions.Banners.Create),
        E("Banners", "Update", "PUT", "/Admin/api/banners/{id}", "banners", "update", AdminRole, AdminPermissions.Banners.Update),
        L("Banners", "Change", "POST", "/Admin/api/banners/{id}/actions", "banners", AdminRole,
            [AdminPermissions.Banners.Archive, AdminPermissions.Banners.Delete, AdminPermissions.Banners.Restore],
            "Permission depends on lifecycle action in the request body."),

        E("Footer", "List", "GET", "/Admin/api/footer", "footer", "read", AdminRole, AdminPermissions.Footer.Read),
        E("Footer", "History", "GET", "/Admin/api/footer/{id}/history", "footer", "read", AdminRole, AdminPermissions.Footer.Read),
        E("Footer", "Create", "POST", "/Admin/api/footer", "footer", "create", AdminRole, AdminPermissions.Footer.Create),
        E("Footer", "Update", "PUT", "/Admin/api/footer/{id}", "footer", "update", AdminRole, AdminPermissions.Footer.Update),
        L("Footer", "Change", "POST", "/Admin/api/footer/{id}/actions", "footer", AdminRole,
            [AdminPermissions.Footer.Archive, AdminPermissions.Footer.Delete, AdminPermissions.Footer.Restore],
            "Permission depends on lifecycle action in the request body."),

        E("Localization", "List", "GET", "/Admin/api/languages", "localization", "read", AdminRole, AdminPermissions.Localization.Read),
        E("Localization", "History", "GET", "/Admin/api/languages/{id}/history", "localization", "read", AdminRole, AdminPermissions.Localization.Read),
        E("Localization", "Create", "POST", "/Admin/api/languages", "localization", "create", AdminRole, AdminPermissions.Localization.Create),
        E("Localization", "Update", "PUT", "/Admin/api/languages/{id}", "localization", "update", AdminRole, AdminPermissions.Localization.Update),
        L("Localization", "Change", "POST", "/Admin/api/languages/{id}/actions", "localization", AdminRole,
            [AdminPermissions.Localization.Archive, AdminPermissions.Localization.Delete, AdminPermissions.Localization.Restore],
            "Permission depends on lifecycle action in the request body."),

        E("Seo", "List", "GET", "/Admin/api/seo", "seo", "read", AdminRole, AdminPermissions.Seo.Read),
        E("Seo", "Targets", "GET", "/Admin/api/seo/targets", "seo", "read", AdminRole, AdminPermissions.Seo.Read),
        E("Seo", "History", "GET", "/Admin/api/seo/{id}/history", "seo", "read", AdminRole, AdminPermissions.Seo.Read),
        E("Seo", "Create", "POST", "/Admin/api/seo", "seo", "create", AdminRole, AdminPermissions.Seo.Create),
        E("Seo", "Update", "PUT", "/Admin/api/seo/{id}", "seo", "update", AdminRole, AdminPermissions.Seo.Update),
        L("Seo", "Change", "POST", "/Admin/api/seo/{id}/actions", "seo", AdminRole,
            [AdminPermissions.Seo.Archive, AdminPermissions.Seo.Delete, AdminPermissions.Seo.Restore],
            "Permission depends on lifecycle action in the request body."),

        E("ContentTemplates", "List", "GET", "/Admin/api/content-templates", "page_templates", "read", AdminRole, AdminPermissions.PageTemplates.Read),
        E("ContentTemplates", "History", "GET", "/Admin/api/content-templates/{id}/history", "page_templates", "read", AdminRole, AdminPermissions.PageTemplates.Read),
        E("ContentTemplates", "Create", "POST", "/Admin/api/content-templates", "page_templates", "create", AdminRole, AdminPermissions.PageTemplates.Create),
        E("ContentTemplates", "Update", "PUT", "/Admin/api/content-templates/{id}", "page_templates", "update", AdminRole, AdminPermissions.PageTemplates.Update),
        L("ContentTemplates", "Change", "POST", "/Admin/api/content-templates/{id}/actions", "page_templates", AdminRole,
            [AdminPermissions.PageTemplates.Archive, AdminPermissions.PageTemplates.Delete, AdminPermissions.PageTemplates.Restore],
            "Permission depends on lifecycle action in the request body."),

        E("PageBlocks", "Render", "POST", "/api/blocks/render", "page_blocks", "read", PublicAdminHelper, AdminPermissions.PageBlocks.Read,
            notes: "Page Builder preview endpoint is currently unauthenticated and is intentionally inventoried for 15.3 hardening."),
        E("PageBlocks", "RenderLayout", "POST", "/api/blocks/render-layout", "page_blocks", "read", PublicAdminHelper, AdminPermissions.PageBlocks.Read,
            notes: "Page Builder preview endpoint is currently unauthenticated and is intentionally inventoried for 15.3 hardening."),

        E("PageBuilderCreate", "Create", "GET", "/Admin/PageBuilder/Create", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageBuilderCreate", "CreateApi", "POST", "/Admin/api/pages", "pages", "create", AdminRole, AdminPermissions.Pages.Create),
        E("PageBuilderCreate", "CreateLegacy", "POST", "/Admin/PageBuilder/Create", "pages", "create", AdminRole, AdminPermissions.Pages.Create),
        E("PageBuilderEdit", "Edit", "GET", "/Admin/PageBuilder/Edit", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageBuilderEdit", "EditPost", "POST", "/Admin/PageBuilder/Edit", "pages", "update", AdminRole, AdminPermissions.Pages.Update),
        E("PageBuilderDelete", "DeleteConfirm", "GET", "/Admin/PageBuilder/DeleteConfirm", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageBuilderDelete", "Delete", "GET", "/Admin/PageBuilder/Delete", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageBuilderDelete", "DeleteApi", "DELETE", "/Admin/api/pages", "pages", "delete", AdminRole, AdminPermissions.Pages.Delete),
        E("PageBuilderDelete", "DeleteConfirmed", "POST", "/Admin/PageBuilder/Delete", "pages", "delete", AdminRole, AdminPermissions.Pages.Delete),
        E("PageHistory", "List", "GET", "/Admin/api/page-history", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageHistory", "History", "GET", "/Admin/api/page-history/{id}", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        L("PageHistory", "Change", "POST", "/Admin/api/page-history/{id}", "pages", AdminRole,
            [AdminPermissions.Pages.Archive, AdminPermissions.Pages.Delete, AdminPermissions.Pages.Restore],
            "Permission depends on page lifecycle action in the request body."),
        E("PageManagement", "List", "GET", "/Admin/api/page-builder/pages", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageManagement", "Get", "GET", "/Admin/api/page-builder/pages/{id}", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("PageManagementMutations", "Create", "POST", "/Admin/api/page-builder/pages", "pages", "create", AdminRole, AdminPermissions.Pages.Create),
        E("PageManagementMutations", "Update", "PUT", "/Admin/api/page-builder/pages/{id}", "pages", "update", AdminRole, AdminPermissions.Pages.Update),
        E("PageManagementMutations", "Delete", "DELETE", "/Admin/api/page-builder/pages/{id}", "pages", "delete", AdminRole, AdminPermissions.Pages.Delete),
        E("PagesApi", "PutLayout", "PUT", "/Admin/api/pages/{id}/layout", "pages", "update", AdminRole, AdminPermissions.Pages.Update),
        E("PagesApi", "GetHead", "GET", "/Admin/api/pages/{id}/head", "pages", "read", AdminRole, AdminPermissions.Pages.Read),
        E("Presets", "List", "GET", "/api/presets", "page_presets", "read", AdminRole, AdminPermissions.PagePresets.Read,
            notes: "Admin-only endpoint lives outside /Admin."),
        E("Presets", "Get", "GET", "/api/presets/{id}", "page_presets", "read", AdminRole, AdminPermissions.PagePresets.Read,
            notes: "Admin-only endpoint lives outside /Admin."),
        E("Presets", "Create", "POST", "/api/presets", "page_presets", "create", AdminRole, AdminPermissions.PagePresets.Create,
            notes: "Admin-only endpoint lives outside /Admin and currently lacks antiforgery validation."),
        E("Presets", "Update", "PUT", "/api/presets/{id}", "page_presets", "update", AdminRole, AdminPermissions.PagePresets.Update,
            notes: "Admin-only endpoint lives outside /Admin and currently lacks antiforgery validation."),
        E("Presets", "Delete", "DELETE", "/api/presets/{id}", "page_presets", "delete", AdminRole, AdminPermissions.PagePresets.Delete,
            notes: "Admin-only endpoint lives outside /Admin and currently lacks antiforgery validation."),
        E("Presets", "Preview", "GET", "/api/presets/{id}/preview", "page_presets", "read", AdminRole, AdminPermissions.PagePresets.Read,
            notes: "Admin-only endpoint lives outside /Admin."),
        E("TalentPages", "List", "GET", "/Admin/api/talent-pages", "talent_pages", "read", AdminRole, AdminPermissions.TalentPages.Read),
        E("TalentPages", "Get", "GET", "/Admin/api/talent-pages/{id}", "talent_pages", "read", AdminRole, AdminPermissions.TalentPages.Read),
        E("TalentPages", "Create", "POST", "/Admin/api/talent-pages", "talent_pages", "create", AdminRole, AdminPermissions.TalentPages.Create),
        E("TalentPages", "Update", "PUT", "/Admin/api/talent-pages/{id}", "talent_pages", "update", AdminRole, AdminPermissions.TalentPages.Update),
        E("TalentsApi", "Save", "POST", "/api/talents/{key}", "talent_trees", "update", AdminRole, AdminPermissions.TalentTrees.Update,
            notes: "Admin-only endpoint lives outside /Admin and currently lacks antiforgery validation."),

        E("Database", "Index", "GET", "/Admin/api/database", "database", "read", AdminRole, AdminPermissions.Database.Read),
        E("Categories", "List", "GET", "/Admin/api/categories", "categories", "read", AdminRole, AdminPermissions.Categories.Read),
        E("Categories", "History", "GET", "/Admin/api/categories/{id}/history", "categories", "read", AdminRole, AdminPermissions.Categories.Read),
        E("Categories", "Create", "POST", "/Admin/api/categories", "categories", "create", AdminRole, AdminPermissions.Categories.Create),
        E("Categories", "Edit", "PUT", "/Admin/api/categories/{id}", "categories", "update", AdminRole, AdminPermissions.Categories.Update),
        E("Categories", "Delete", "DELETE", "/Admin/api/categories/{id}", "categories", "delete", AdminRole, AdminPermissions.Categories.Delete),
        E("Categories", "Restore", "POST", "/Admin/api/categories/{id}/restore", "categories", "restore", AdminRole, AdminPermissions.Categories.Restore),
        E("Classes", "List", "GET", "/Admin/api/classes", "classes", "read", AdminRole, AdminPermissions.Classes.Read),
        E("Classes", "History", "GET", "/Admin/api/classes/{id}/history", "classes", "read", AdminRole, AdminPermissions.Classes.Read),
        E("Classes", "Create", "POST", "/Admin/api/classes", "classes", "create", AdminRole, AdminPermissions.Classes.Create,
            notes: "The same hierarchy represents classes and specializations."),
        E("Classes", "Edit", "PUT", "/Admin/api/classes/{id}", "classes", "update", AdminRole, AdminPermissions.Classes.Update,
            notes: "The same hierarchy represents classes and specializations."),
        E("Classes", "Delete", "DELETE", "/Admin/api/classes/{id}", "classes", "delete", AdminRole, AdminPermissions.Classes.Delete),
        E("Classes", "Restore", "POST", "/Admin/api/classes/{id}/restore", "classes", "restore", AdminRole, AdminPermissions.Classes.Restore),
        E("Tags", "List", "GET", "/Admin/api/tags", "tags", "read", AdminRole, AdminPermissions.Tags.Read),
        E("Tags", "History", "GET", "/Admin/api/tags/{id}/history", "tags", "read", AdminRole, AdminPermissions.Tags.Read),
        E("Tags", "Create", "POST", "/Admin/api/tags", "tags", "create", AdminRole, AdminPermissions.Tags.Create),
        E("Tags", "Edit", "PUT", "/Admin/api/tags/{id}", "tags", "update", AdminRole, AdminPermissions.Tags.Update),
        E("Tags", "Delete", "DELETE", "/Admin/api/tags/{id}", "tags", "delete", AdminRole, AdminPermissions.Tags.Delete),
        E("Tags", "Restore", "POST", "/Admin/api/tags/{id}/restore", "tags", "restore", AdminRole, AdminPermissions.Tags.Restore),
        E("Patches", "List", "GET", "/Admin/api/patches", "patches", "read", AdminRole, AdminPermissions.Patches.Read),
        E("Patches", "History", "GET", "/Admin/api/patches/{id}/history", "patches", "read", AdminRole, AdminPermissions.Patches.Read),
        E("Patches", "Create", "POST", "/Admin/api/patches", "patches", "create", AdminRole, AdminPermissions.Patches.Create),
        E("Patches", "Edit", "PUT", "/Admin/api/patches/{id}", "patches", "update", AdminRole, AdminPermissions.Patches.Update),
        E("Patches", "Delete", "DELETE", "/Admin/api/patches/{id}", "patches", "delete", AdminRole, AdminPermissions.Patches.Delete),
        E("Patches", "Restore", "POST", "/Admin/api/patches/{id}/restore", "patches", "restore", AdminRole, AdminPermissions.Patches.Restore),
        E("Rarities", "List", "GET", "/Admin/api/rarities", "rarities", "read", AdminRole, AdminPermissions.Rarities.Read),
        E("Rarities", "History", "GET", "/Admin/api/rarities/{id}/history", "rarities", "read", AdminRole, AdminPermissions.Rarities.Read),
        E("Rarities", "Create", "POST", "/Admin/api/rarities", "rarities", "create", AdminRole, AdminPermissions.Rarities.Create),
        E("Rarities", "Edit", "PUT", "/Admin/api/rarities/{id}", "rarities", "update", AdminRole, AdminPermissions.Rarities.Update),
        E("Rarities", "Delete", "DELETE", "/Admin/api/rarities/{id}", "rarities", "delete", AdminRole, AdminPermissions.Rarities.Delete),
        E("Rarities", "Restore", "POST", "/Admin/api/rarities/{id}/restore", "rarities", "restore", AdminRole, AdminPermissions.Rarities.Restore),
        E("RecordTypes", "List", "GET", "/Admin/api/record-types", "record_types", "read", AdminRole, AdminPermissions.RecordTypes.Read),
        E("RecordTypes", "Create", "POST", "/Admin/api/record-types", "record_types", "create", AdminRole, AdminPermissions.RecordTypes.Create),
        E("RecordTypes", "Rename", "PUT", "/Admin/api/record-types", "record_types", "update", AdminRole, AdminPermissions.RecordTypes.Update),
        E("RecordTypes", "Delete", "DELETE", "/Admin/api/record-types", "record_types", "delete", AdminRole, AdminPermissions.RecordTypes.Delete),
        E("Media", "List", "GET", "/Admin/api/media", "media", "read", AdminRole, AdminPermissions.Media.Read),
        E("Media", "History", "GET", "/Admin/api/media/{id}/history", "media", "read", AdminRole, AdminPermissions.Media.Read),
        E("Media", "Edit", "PUT", "/Admin/api/media/{id}", "media", "update", AdminRole, AdminPermissions.Media.Update),
        E("Media", "Delete", "DELETE", "/Admin/api/media/{id}", "media", "delete", AdminRole, AdminPermissions.Media.Delete),
        E("Media", "Restore", "POST", "/Admin/api/media/{id}/restore", "media", "restore", AdminRole, AdminPermissions.Media.Restore),
        E("Navigation", "List", "GET", "/Admin/api/navigation", "navigation", "read", AdminRole, AdminPermissions.Navigation.Read),
        E("Navigation", "History", "GET", "/Admin/api/navigation/{id}/history", "navigation", "read", AdminRole, AdminPermissions.Navigation.Read),
        E("Navigation", "Create", "POST", "/Admin/api/navigation", "navigation", "create", AdminRole, AdminPermissions.Navigation.Create),
        E("Navigation", "Edit", "PUT", "/Admin/api/navigation/{id}", "navigation", "update", AdminRole, AdminPermissions.Navigation.Update),
        E("Navigation", "Delete", "DELETE", "/Admin/api/navigation/{id}", "navigation", "delete", AdminRole, AdminPermissions.Navigation.Delete),
        E("Navigation", "Restore", "POST", "/Admin/api/navigation/{id}/restore", "navigation", "restore", AdminRole, AdminPermissions.Navigation.Restore),
        E("SpellIcons", "Browse", "GET", "/Admin/api/spells/icons", "spell_icons", "read", AdminRole, AdminPermissions.SpellIcons.Read),
        E("SpellIcons", "Upload", "POST", "/Admin/api/spells/icons", "spell_icons", "create", AdminRole, AdminPermissions.SpellIcons.Create),

        E("Items", "Create", "GET", "/Admin/api/items/create", "items", "read", AdminRole, AdminPermissions.Items.Read),
        E("Items", "Edit", "GET", "/Admin/api/items/{id}/edit", "items", "read", AdminRole, AdminPermissions.Items.Read),
        E("Items", "Details", "GET", "/Admin/api/items/{id}", "items", "read", AdminRole, AdminPermissions.Items.Read),
        E("Items", "Delete", "GET", "/Admin/api/items/{id}/delete", "items", "read", AdminRole, AdminPermissions.Items.Read),
        E("ItemMutations", "Create", "POST", "/Admin/api/items", "items", "create", AdminRole, AdminPermissions.Items.Create),
        E("ItemMutations", "Edit", "PUT", "/Admin/api/items/{id}", "items", "update", AdminRole, AdminPermissions.Items.Update),
        E("ItemMutations", "DeleteConfirmed", "DELETE", "/Admin/api/items/{id}", "items", "delete", AdminRole, AdminPermissions.Items.Delete),

        E("Spells", "Create", "GET", "/Admin/api/spells/create", "spells", "read", AdminRole, AdminPermissions.Spells.Read),
        E("Spells", "Edit", "GET", "/Admin/api/spells/{id}/edit", "spells", "read", AdminRole, AdminPermissions.Spells.Read),
        E("Spells", "Details", "GET", "/Admin/api/spells/{id}", "spells", "read", AdminRole, AdminPermissions.Spells.Read),
        E("Spells", "Delete", "GET", "/Admin/api/spells/{id}/delete", "spells", "read", AdminRole, AdminPermissions.Spells.Read),
        E("SpellMutations", "Create", "POST", "/Admin/api/spells", "spells", "create", AdminRole, AdminPermissions.Spells.Create),
        E("SpellMutations", "Edit", "PUT", "/Admin/api/spells/{id}", "spells", "update", AdminRole, AdminPermissions.Spells.Update),
        E("SpellMutations", "DeleteConfirmed", "DELETE", "/Admin/api/spells/{id}", "spells", "delete", AdminRole, AdminPermissions.Spells.Delete),

        E("CartArchive", "Archive", "GET", "/api/cart/archive | /Cart/archive", "carts", "read", AdminRole, AdminPermissions.Carts.Read,
            notes: "Admin-only archived cart/order view lives outside /Admin."),
        E("CartArchive", "Details", "GET", "/api/cart/archive/{id} | /Cart/archive/{id} | /api/cart/Details/{id} | /Cart/Details/{id}", "carts", "read", AdminRole, AdminPermissions.Carts.Read,
            notes: "Admin-only archived cart/order details live outside /Admin."),

        E("ProductCreate", "Create", "GET", "/api/products/Create | /Products/Create", "products", "read", AdminRole, AdminPermissions.Products.Read,
            notes: "Admin-only product endpoint lives outside /Admin and has API/legacy route aliases."),
        E("ProductCreate", "CreateApi", "POST", "/api/products | /Products", "products", "create", AdminRole, AdminPermissions.Products.Create,
            notes: "Admin-only product endpoint lives outside /Admin."),
        E("ProductCreate", "CreateLegacy", "POST", "/api/products/Create | /Products/Create", "products", "create", AdminRole, AdminPermissions.Products.Create),
        E("ProductEdit", "EditApi", "GET", "/api/products/{id}/edit | /Products/{id}/edit", "products", "read", AdminRole, AdminPermissions.Products.Read),
        E("ProductEdit", "EditLegacy", "GET", "/api/products/Edit | /Products/Edit", "products", "read", AdminRole, AdminPermissions.Products.Read),
        E("ProductEdit", "EditApi#PUT", "PUT", "/api/products/{id} | /Products/{id}", "products", "update", AdminRole, AdminPermissions.Products.Update,
            notes: "Registry key suffix distinguishes the overloaded controller action."),
        E("ProductEdit", "EditLegacy#POST", "POST", "/api/products/Edit | /Products/Edit", "products", "update", AdminRole, AdminPermissions.Products.Update,
            notes: "Registry key suffix distinguishes the overloaded controller action."),
        E("ProductDelete", "DeleteApi", "DELETE", "/api/products/{id} | /Products/{id}", "products", "delete", AdminRole, AdminPermissions.Products.Delete),
        E("ProductDelete", "DeleteLegacy", "GET", "/api/products/DeleteProduct | /Products/DeleteProduct", "products", "delete", AdminRole, AdminPermissions.Products.Delete,
            notes: "Legacy destructive GET must be removed or converted during 15.3 hardening."),
        E("PromoCodes", "Index", "GET", "/Admin/api/promo-codes", "promo_codes", "read", AdminRole, AdminPermissions.PromoCodes.Read),
        E("PromoCodes", "Create", "GET", "/Admin/api/promo-codes/create | /Admin/PromoCodes/Create", "promo_codes", "read", AdminRole, AdminPermissions.PromoCodes.Read),
        E("PromoCodes", "CreateApi", "POST", "/Admin/api/promo-codes", "promo_codes", "create", AdminRole, AdminPermissions.PromoCodes.Create),
        E("PromoCodes", "CreateLegacy", "POST", "/Admin/PromoCodes/Create", "promo_codes", "create", AdminRole, AdminPermissions.PromoCodes.Create),
        E("PromoCodes", "DeactivateApi", "POST", "/Admin/api/promo-codes/{id}/deactivate", "promo_codes", "update", AdminRole, AdminPermissions.PromoCodes.Update),
        E("PromoCodes", "DeactivateLegacy", "POST", "/Admin/PromoCodes/Deactivate", "promo_codes", "update", AdminRole, AdminPermissions.PromoCodes.Update),

        E("Products", "DetailsApi", "GET", "/api/products/{id}", "products", "read", Authenticated, AdminPermissions.Products.Read,
            mixed: true, notes: "Public detail action has an Admin-only visibility override for otherwise hidden product state."),
        E("Products", "DetailsLegacy", "GET", "/Products/Details", "products", "read", Authenticated, AdminPermissions.Products.Read,
            mixed: true, notes: "Public detail action has an Admin-only visibility override for otherwise hidden product state."),
        E("ProductReviews", "DeleteReviewApi", "DELETE", "/api/products/{productId}/reviews/{reviewId}", "product_reviews", "delete", Authenticated, AdminPermissions.ProductReviews.Delete,
            mixed: true, notes: "Owners may delete their own reviews; administrative override currently uses User.IsInRole(\"Admin\")."),
        E("ProductReviews", "DeleteReviewLegacy", "POST", "/Products/DeleteReview | /api/products/DeleteReview", "product_reviews", "delete", Authenticated, AdminPermissions.ProductReviews.Delete,
            mixed: true, notes: "Owners may delete their own reviews; administrative override currently uses User.IsInRole(\"Admin\").")
    ];

    public static IReadOnlyList<AdminEndpointDefinition> All => Definitions;

    public static IReadOnlyDictionary<string, IReadOnlyList<AdminEndpointDefinition>> ByAction { get; } =
        Definitions
            .GroupBy(definition => definition.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AdminEndpointDefinition>)group.ToArray(),
                StringComparer.Ordinal);

    public static bool TryGet(string controller, string action, out IReadOnlyList<AdminEndpointDefinition> endpoints)
    {
        string key = $"{controller}.{action}";
        return ByAction.TryGetValue(key, out endpoints!);
    }

    private static AdminEndpointDefinition E(
        string controller,
        string action,
        string method,
        string route,
        string resource,
        string operation,
        string protection,
        string permission,
        bool mixed = false,
        string? notes = null)
    {
        return new AdminEndpointDefinition(
            controller,
            action,
            method,
            route,
            resource,
            operation,
            protection,
            [permission],
            mixed,
            notes);
    }

    private static AdminEndpointDefinition L(
        string controller,
        string action,
        string method,
        string route,
        string resource,
        string protection,
        IReadOnlyList<string> permissions,
        string notes)
    {
        return new AdminEndpointDefinition(
            controller,
            action,
            method,
            route,
            resource,
            "lifecycle",
            protection,
            permissions,
            false,
            notes);
    }
}
