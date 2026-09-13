namespace PaladinHubV2.Server.Domain.Services.Seo;

public enum SeoRouteVisibility
{
    PublicIndexable,
    PublicNonIndexable,
    Private,
    Admin,
    DynamicPublic,
    DatabasePage
}

public sealed record SeoRouteDefinition(
    string Route,
    string? CanonicalPath,
    SeoRouteVisibility Visibility,
    bool Selectable,
    bool IsAlias = false);

public static class SeoRouteRegistry
{
    public const string Version = "2026-09-13.1";

    private static readonly SeoRouteDefinition[] Routes =
    [
        Public("/"),
        Alias("/Home/Home", "/"),

        Public("/Holy/Overview"),
        Public("/Holy/Gear"),
        Public("/Holy/Talents"),
        Public("/Holy/Consumables"),
        Public("/Holy/Rotation"),
        Public("/Holy/Stats"),
        Public("/Protection/Overview"),
        Public("/Protection/Gear"),
        Public("/Protection/Talents"),
        Public("/Protection/Consumables"),
        Public("/Protection/Rotation"),
        Public("/Protection/Stats"),
        Public("/Retribution/Overview"),
        Public("/Retribution/Gear"),
        Public("/Retribution/Talents"),
        Public("/Retribution/Consumables"),
        Public("/Retribution/Rotation"),
        Public("/Retribution/Stats"),

        Public("/discussions"),
        Alias("/Discussion/Index", "/discussions"),
        Alias("/Discussions/Index", "/discussions"),
        Dynamic("/Discussions/Details/:id"),
        NonIndexable("/Discussions/Create"),

        Public("/products"),
        Alias("/Merchandise/Merchandise", "/products"),
        Alias("/Merchandise/List", "/products"),
        Dynamic("/Products/Details/:id"),
        Dynamic("/products/:id"),
        Private("/Products/Add/:id"),
        Admin("/Products/Create"),
        Admin("/Products/Edit/:id"),

        Public("/privacy"),
        Alias("/Home/Privacy", "/privacy"),
        NonIndexable("/Error/404"),
        NonIndexable("/Error/500"),
        NonIndexable("/error"),

        Private("/Cart/MyCart"),
        Private("/cart"),
        Private("/Cart/Details/:id"),
        Private("/Cart/Archive"),
        Private("/Checkout/Start"),
        Private("/checkout"),
        Private("/Checkout/Shipping"),
        Private("/Checkout/Payment"),
        Private("/Checkout/Card"),
        Private("/Checkout/Review"),
        Private("/Checkout/Registered"),
        Private("/Checkout/Success"),
        Private("/Checkout/Failure"),
        Private("/Home/ThanksForPurchasing"),

        Private("/Account/Login"),
        Private("/login"),
        Private("/Account/Register"),
        Private("/register"),
        Private("/Account/LoginWith2fa"),
        Private("/Account/RecoveryCodeLogin"),
        Private("/Account/VerifyEmail"),
        Private("/Account/ShowRecoveryCodes"),
        Private("/Account/MyAccount"),
        Private("/account"),
        Private("/Account/AccountDetails"),
        Private("/Account/ChangePassword"),
        Private("/Account/Connections"),
        Private("/Account/Enable2FA"),
        Private("/Account/PaymentMethods"),
        Private("/Account/AddPaymentMethod"),
        Private("/Account/Privacy"),
        Private("/Account/Security"),
        Private("/Account/Settings"),
        Private("/Account/TransactionHistory"),

        Admin("/Admin"),
        Admin("/Admin/Database"),
        Admin("/Admin/Categories"),
        Admin("/Admin/Classes"),
        Admin("/Admin/PageBuilder/History"),
        Admin("/Admin/Footer"),
        Admin("/Admin/Banners"),
        Admin("/Admin/Translations"),
        Admin("/Admin/Navigation"),
        Admin("/Admin/Media"),
        Admin("/Admin/Rarities"),
        Admin("/Admin/Patches"),
        Admin("/Admin/Tags"),
        Admin("/Admin/Database/Index"),
        Admin("/Admin/Items/Create"),
        Admin("/Admin/Items/Edit/:id"),
        Admin("/Admin/Items/Details/:id"),
        Admin("/Admin/Items/Delete/:id"),
        Admin("/Admin/Spells/Create"),
        Admin("/Admin/Spells/Edit/:id"),
        Admin("/Admin/Spells/Details/:id"),
        Admin("/Admin/Spells/Delete/:id"),
        Admin("/Admin/PageBuilder"),
        Admin("/Admin/PageBuilder/Index"),
        Admin("/Admin/PageBuilder/TalentTrees"),
        Admin("/Admin/PageBuilder/Create"),
        Admin("/Admin/PageBuilder/Edit"),
        Admin("/Admin/PageBuilder/DeleteConfirm"),
        Admin("/Admin/PageBuilder/Delete"),
        Admin("/Admin/Products/Create"),
        Admin("/Admin/Products/Edit/:id"),
        Admin("/Admin/PromoCodes"),
        Admin("/Admin/PromoCodes/Index"),
        Admin("/Admin/PromoCodes/Create"),

        new("/:section/:slug", null, SeoRouteVisibility.DatabasePage, false)
    ];

    public static IReadOnlyList<SeoRouteDefinition> All => Routes;

    public static IReadOnlyList<SeoRouteDefinition> StaticSeoTargets =>
        Routes.Where(route => route.Selectable).ToArray();

    public static bool TryResolveStaticTarget(
        string? value,
        out SeoRouteDefinition? route,
        out string? error)
    {
        route = null;
        if (!TryNormalizePath(value, allowGlobal: true, out string normalized, out error))
        {
            return false;
        }

        if (normalized == "*")
        {
            route = new SeoRouteDefinition(
                "*",
                null,
                SeoRouteVisibility.PublicIndexable,
                true);
            return true;
        }

        SeoRouteDefinition? exact = Routes.FirstOrDefault(candidate =>
            candidate.Route.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (exact is null)
        {
            error = "Choose an existing public static route or a database page.";
            return false;
        }

        if (!exact.Selectable)
        {
            error = exact.IsAlias && exact.CanonicalPath is not null
                ? $"Use the canonical route '{exact.CanonicalPath}' instead of this alias."
                : "This route is not an SEO-configurable public static page.";
            return false;
        }

        route = exact;
        error = null;
        return true;
    }

    public static bool TryNormalizePath(
        string? value,
        bool allowGlobal,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "A route is required.";
            return false;
        }

        string candidate = value.Trim();
        if (allowGlobal && candidate == "*")
        {
            normalized = "*";
            return true;
        }

        if (candidate.Length > 2048 || candidate.Any(char.IsControl))
        {
            error = "The route is too long or contains control characters.";
            return false;
        }

        if (!candidate.StartsWith('/') ||
            candidate.StartsWith("//", StringComparison.Ordinal) ||
            candidate.Contains("//", StringComparison.Ordinal) ||
            candidate.Contains('\\') ||
            candidate.Contains('?') ||
            candidate.Contains('#') ||
            candidate.Contains('%'))
        {
            error = "The route must be a plain local path without encoding, query, fragment, backslashes or repeated separators.";
            return false;
        }

        candidate = candidate.Length > 1
            ? candidate.TrimEnd('/')
            : candidate;

        string[] segments = candidate.Split('/', StringSplitOptions.None);
        if (segments.Skip(1).Any(segment =>
                string.IsNullOrEmpty(segment) ||
                segment is "." or ".."))
        {
            error = "The route contains an invalid path segment.";
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static bool TryBuildDatabasePagePath(
        string section,
        string slug,
        out string path,
        out string? error)
    {
        return TryNormalizePath(
            $"/{section.Trim('/')}/{slug.Trim('/')}",
            allowGlobal: false,
            out path,
            out error);
    }

    private static SeoRouteDefinition Public(string route) =>
        new(route, route, SeoRouteVisibility.PublicIndexable, true);

    private static SeoRouteDefinition Alias(string route, string canonical) =>
        new(route, canonical, SeoRouteVisibility.PublicIndexable, false, true);

    private static SeoRouteDefinition NonIndexable(string route) =>
        new(route, null, SeoRouteVisibility.PublicNonIndexable, false);

    private static SeoRouteDefinition Private(string route) =>
        new(route, null, SeoRouteVisibility.Private, false);

    private static SeoRouteDefinition Admin(string route) =>
        new(route, null, SeoRouteVisibility.Admin, false);

    private static SeoRouteDefinition Dynamic(string route) =>
        new(route, null, SeoRouteVisibility.DynamicPublic, false);
}
