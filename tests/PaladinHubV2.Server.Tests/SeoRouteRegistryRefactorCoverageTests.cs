using PaladinHubV2.Server.Domain.Services.Seo;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoRouteRegistryRefactorCoverageTests
{
    [Fact]
    public void CatalogExposesAllRouteKindsAndOnlySelectableStaticTargets()
    {
        var catalog = new SeoRouteCatalog();

        Assert.NotEmpty(catalog.All);
        Assert.NotEmpty(catalog.StaticSeoTargets);

        Assert.Contains(
            catalog.All,
            route =>
                route.Route == "/" &&
                route.Visibility ==
                    SeoRouteVisibility.PublicIndexable &&
                route.Selectable &&
                !route.IsAlias);

        SeoRouteDefinition alias =
            Assert.Single(
                catalog.All,
                route =>
                    route.Route ==
                    "/Home/Home");

        Assert.True(alias.IsAlias);
        Assert.False(alias.Selectable);
        Assert.Equal("/", alias.CanonicalPath);

        Assert.Contains(
            catalog.All,
            route =>
                route.Visibility ==
                SeoRouteVisibility.PublicNonIndexable);

        Assert.Contains(
            catalog.All,
            route =>
                route.Visibility ==
                SeoRouteVisibility.Private);

        Assert.Contains(
            catalog.All,
            route =>
                route.Visibility ==
                SeoRouteVisibility.Admin);

        Assert.Contains(
            catalog.All,
            route =>
                route.Visibility ==
                SeoRouteVisibility.DynamicPublic);

        Assert.Contains(
            catalog.All,
            route =>
                route.Route ==
                    "/:section/:slug" &&
                route.Visibility ==
                    SeoRouteVisibility.DatabasePage &&
                !route.Selectable);

        Assert.All(
            catalog.StaticSeoTargets,
            route => Assert.True(route.Selectable));

        Assert.DoesNotContain(
            catalog.StaticSeoTargets,
            route => route.IsAlias);

        Assert.Equal(
            catalog.All.Count(route =>
                route.Selectable),
            catalog.StaticSeoTargets.Count);
    }

    [Fact]
    public void PathPolicyCoversGlobalValidationNormalizationAndDatabasePaths()
    {
        var policy = new SeoRoutePathPolicy();

        Assert.False(
            policy.TryNormalizePath(
                null,
                true,
                out string missing,
                out string? missingError));

        Assert.Equal(string.Empty, missing);
        Assert.Equal(
            "A route is required.",
            missingError);

        Assert.False(
            policy.TryNormalizePath(
                "   ",
                true,
                out _,
                out _));

        Assert.True(
            policy.TryNormalizePath(
                " * ",
                true,
                out string global,
                out string? globalError));

        Assert.Equal("*", global);
        Assert.Null(globalError);

        Assert.False(
            policy.TryNormalizePath(
                "*",
                false,
                out _,
                out string? noGlobalError));

        Assert.Equal(
            "The route must be a plain local path without encoding, query, fragment, backslashes or repeated separators.",
            noGlobalError);

        Assert.False(
            policy.TryNormalizePath(
                "/" + new string('a', 2048),
                false,
                out _,
                out string? tooLongError));

        Assert.Equal(
            "The route is too long or contains control characters.",
            tooLongError);

        Assert.False(
            policy.TryNormalizePath(
                "/Holy/\nOverview",
                false,
                out _,
                out string? controlError));

        Assert.Equal(
            "The route is too long or contains control characters.",
            controlError);

        foreach (string invalid in new[]
        {
            "Holy/Overview",
            "//Holy/Overview",
            "/Holy//Overview",
            "/Holy\\Overview",
            "/Holy/Overview?preview=true",
            "/Holy/Overview#fragment",
            "/Holy/%2e%2e/Overview"
        })
        {
            Assert.False(
                policy.TryNormalizePath(
                    invalid,
                    false,
                    out _,
                    out string? error));

            Assert.Equal(
                "The route must be a plain local path without encoding, query, fragment, backslashes or repeated separators.",
                error);
        }

        Assert.True(
            policy.TryNormalizePath(
                "/",
                false,
                out string root,
                out string? rootError));

        Assert.Equal("/", root);
        Assert.Null(rootError);

        Assert.True(
            policy.TryNormalizePath(
                " /Holy/Overview/ ",
                false,
                out string normalized,
                out string? normalizedError));

        Assert.Equal(
            "/Holy/Overview",
            normalized);

        Assert.Null(normalizedError);

        foreach (string invalidSegment in new[]
        {
            "/Holy/./Overview",
            "/Holy/../Overview"
        })
        {
            Assert.False(
                policy.TryNormalizePath(
                    invalidSegment,
                    false,
                    out _,
                    out string? error));

            Assert.Equal(
                "The route contains an invalid path segment.",
                error);
        }

        Assert.True(
            policy.TryBuildDatabasePagePath(
                "/Guides/",
                "/holy-paladin/",
                out string pagePath,
                out string? pageError));

        Assert.Equal(
            "/Guides/holy-paladin",
            pagePath);

        Assert.Null(pageError);

        Assert.False(
            policy.TryBuildDatabasePagePath(
                ".",
                "holy-paladin",
                out _,
                out string? invalidPageError));

        Assert.Equal(
            "The route contains an invalid path segment.",
            invalidPageError);
    }

    [Fact]
    public void ResolverCoversGlobalCanonicalUnknownAliasAndNonSelectableTargets()
    {
        var resolver =
            new SeoStaticRouteResolver(
                new SeoRouteCatalog(),
                new SeoRoutePathPolicy());

        Assert.False(
            resolver.TryResolveStaticTarget(
                null,
                out SeoRouteDefinition? missing,
                out string? missingError));

        Assert.Null(missing);
        Assert.Equal(
            "A route is required.",
            missingError);

        Assert.True(
            resolver.TryResolveStaticTarget(
                "*",
                out SeoRouteDefinition? global,
                out string? globalError));

        Assert.NotNull(global);
        Assert.Equal("*", global!.Route);
        Assert.Null(global.CanonicalPath);
        Assert.Equal(
            SeoRouteVisibility.PublicIndexable,
            global.Visibility);
        Assert.True(global.Selectable);
        Assert.Null(globalError);

        Assert.True(
            resolver.TryResolveStaticTarget(
                "/HOLY/OVERVIEW/",
                out SeoRouteDefinition? canonical,
                out string? canonicalError));

        Assert.NotNull(canonical);
        Assert.Equal(
            "/Holy/Overview",
            canonical!.Route);
        Assert.Null(canonicalError);

        Assert.False(
            resolver.TryResolveStaticTarget(
                "/not-registered",
                out SeoRouteDefinition? unknown,
                out string? unknownError));

        Assert.Null(unknown);
        Assert.Equal(
            "Choose an existing public static route or a database page.",
            unknownError);

        Assert.False(
            resolver.TryResolveStaticTarget(
                "/Home/Home",
                out SeoRouteDefinition? alias,
                out string? aliasError));

        Assert.Null(alias);
        Assert.Equal(
            "Use the canonical route '/' instead of this alias.",
            aliasError);

        Assert.False(
            resolver.TryResolveStaticTarget(
                "/Account/Login",
                out SeoRouteDefinition? privateRoute,
                out string? privateError));

        Assert.Null(privateRoute);
        Assert.Equal(
            "This route is not an SEO-configurable public static page.",
            privateError);
    }

    [Fact]
    public void CompatibilityFacadeDelegatesCatalogResolutionAndPathPolicy()
    {
        Assert.Equal(
            "2026-09-13.1",
            SeoRouteRegistry.Version);

        Assert.NotEmpty(
            SeoRouteRegistry.All);

        Assert.NotEmpty(
            SeoRouteRegistry.StaticSeoTargets);

        Assert.True(
            SeoRouteRegistry.TryResolveStaticTarget(
                "/products/",
                out SeoRouteDefinition? route,
                out string? resolveError));

        Assert.Equal(
            "/products",
            route!.Route);

        Assert.Null(resolveError);

        Assert.True(
            SeoRouteRegistry.TryNormalizePath(
                "/privacy/",
                false,
                out string normalized,
                out string? normalizeError));

        Assert.Equal(
            "/privacy",
            normalized);

        Assert.Null(normalizeError);

        Assert.True(
            SeoRouteRegistry.TryBuildDatabasePagePath(
                "Guides",
                "rotation",
                out string pagePath,
                out string? pageError));

        Assert.Equal(
            "/Guides/rotation",
            pagePath);

        Assert.Null(pageError);
    }
}
