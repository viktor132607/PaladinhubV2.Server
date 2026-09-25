namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed class SeoStaticRouteResolver :
    ISeoStaticRouteResolver
{
    private readonly ISeoRouteCatalog _catalog;
    private readonly ISeoRoutePathPolicy _pathPolicy;

    public SeoStaticRouteResolver(
        ISeoRouteCatalog catalog,
        ISeoRoutePathPolicy pathPolicy)
    {
        _catalog = catalog;
        _pathPolicy = pathPolicy;
    }

    public bool TryResolveStaticTarget(
        string? value,
        out SeoRouteDefinition? route,
        out string? error)
    {
        route = null;

        if (!_pathPolicy.TryNormalizePath(
                value,
                allowGlobal: true,
                out string normalized,
                out error))
        {
            return false;
        }

        if (normalized == "*")
        {
            route =
                new SeoRouteDefinition(
                    "*",
                    null,
                    SeoRouteVisibility.PublicIndexable,
                    true);

            return true;
        }

        SeoRouteDefinition? exact =
            _catalog.All.FirstOrDefault(
                candidate =>
                    candidate.Route.Equals(
                        normalized,
                        StringComparison.OrdinalIgnoreCase));

        if (exact is null)
        {
            error =
                "Choose an existing public static route or a database page.";
            return false;
        }

        if (!exact.Selectable)
        {
            error =
                exact.IsAlias &&
                exact.CanonicalPath is not null
                    ? $"Use the canonical route '{exact.CanonicalPath}' instead of this alias."
                    : "This route is not an SEO-configurable public static page.";

            return false;
        }

        route = exact;
        error = null;

        return true;
    }
}
