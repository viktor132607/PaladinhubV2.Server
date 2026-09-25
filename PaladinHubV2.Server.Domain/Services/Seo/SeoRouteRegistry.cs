namespace PaladinHubV2.Server.Domain.Services.Seo;

public static class SeoRouteRegistry
{
    public const string Version = "2026-09-13.1";

    private static readonly SeoRouteCatalog Catalog =
        new();

    private static readonly SeoRoutePathPolicy PathPolicy =
        new();

    private static readonly SeoStaticRouteResolver Resolver =
        new(
            Catalog,
            PathPolicy);

    public static IReadOnlyList<SeoRouteDefinition> All =>
        Catalog.All;

    public static IReadOnlyList<SeoRouteDefinition> StaticSeoTargets =>
        Catalog.StaticSeoTargets;

    public static bool TryResolveStaticTarget(
        string? value,
        out SeoRouteDefinition? route,
        out string? error)
    {
        return Resolver.TryResolveStaticTarget(
            value,
            out route,
            out error);
    }

    public static bool TryNormalizePath(
        string? value,
        bool allowGlobal,
        out string normalized,
        out string? error)
    {
        return PathPolicy.TryNormalizePath(
            value,
            allowGlobal,
            out normalized,
            out error);
    }

    public static bool TryBuildDatabasePagePath(
        string section,
        string slug,
        out string path,
        out string? error)
    {
        return PathPolicy.TryBuildDatabasePagePath(
            section,
            slug,
            out path,
            out error);
    }
}
