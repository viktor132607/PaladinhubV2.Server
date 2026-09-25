namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed class SeoRouteCatalog :
    ISeoRouteCatalog
{
    private readonly SeoRouteDefinition[] _routes;
    private readonly SeoRouteDefinition[] _staticSeoTargets;

    public SeoRouteCatalog()
    {
        _routes =
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
            Alias(
                "/Discussion/Index",
                "/discussions"),
            Alias(
                "/Discussions/Index",
                "/discussions"),
            Dynamic("/Discussions/Details/:id"),
            NonIndexable("/Discussions/Create"),

            Public("/products"),
            Alias(
                "/Merchandise/Merchandise",
                "/products"),
            Alias(
                "/Merchandise/List",
                "/products"),
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

            new SeoRouteDefinition(
                "/:section/:slug",
                null,
                SeoRouteVisibility.DatabasePage,
                false)
        ];

        _staticSeoTargets =
            _routes
                .Where(route => route.Selectable)
                .ToArray();
    }

    public IReadOnlyList<SeoRouteDefinition> All =>
        _routes;

    public IReadOnlyList<SeoRouteDefinition> StaticSeoTargets =>
        _staticSeoTargets;

    private static SeoRouteDefinition Public(
        string route) =>
        new(
            route,
            route,
            SeoRouteVisibility.PublicIndexable,
            true);

    private static SeoRouteDefinition Alias(
        string route,
        string canonical) =>
        new(
            route,
            canonical,
            SeoRouteVisibility.PublicIndexable,
            false,
            true);

    private static SeoRouteDefinition NonIndexable(
        string route) =>
        new(
            route,
            null,
            SeoRouteVisibility.PublicNonIndexable,
            false);

    private static SeoRouteDefinition Private(
        string route) =>
        new(
            route,
            null,
            SeoRouteVisibility.Private,
            false);

    private static SeoRouteDefinition Admin(
        string route) =>
        new(
            route,
            null,
            SeoRouteVisibility.Admin,
            false);

    private static SeoRouteDefinition Dynamic(
        string route) =>
        new(
            route,
            null,
            SeoRouteVisibility.DynamicPublic,
            false);
}
