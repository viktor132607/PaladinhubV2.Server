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

public interface ISeoRouteCatalog
{
    IReadOnlyList<SeoRouteDefinition> All { get; }

    IReadOnlyList<SeoRouteDefinition> StaticSeoTargets { get; }
}

public interface ISeoRoutePathPolicy
{
    bool TryNormalizePath(
        string? value,
        bool allowGlobal,
        out string normalized,
        out string? error);

    bool TryBuildDatabasePagePath(
        string section,
        string slug,
        out string path,
        out string? error);
}

public interface ISeoStaticRouteResolver
{
    bool TryResolveStaticTarget(
        string? value,
        out SeoRouteDefinition? route,
        out string? error);
}
