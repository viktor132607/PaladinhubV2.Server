using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Seo;

public interface ISeoTargetResolver
{
    Task<SeoResolvedTargetResult> ResolveAndValidateRelationsAsync(
        Guid currentId,
        SeoRequest request,
        CancellationToken ct);

    Task<SeoResolvedTarget?> ResolveExistingTargetAsync(
        SeoEntry entry,
        CancellationToken ct);
}

public sealed class SeoTargetResolver : ISeoTargetResolver
{
    private readonly AppDbContext _db;

    public SeoTargetResolver(AppDbContext db)
    {
        _db = db;
    }

    private DbSet<SeoEntry> Entries => _db.Set<SeoEntry>();

    public async Task<SeoResolvedTargetResult> ResolveAndValidateRelationsAsync(
        Guid currentId,
        SeoRequest request,
        CancellationToken ct)
    {
        SeoResolvedTargetResult targetResult = await ResolveTargetAsync(request, ct);
        if (targetResult.Error is not null)
        {
            return targetResult;
        }

        SeoResolvedTarget target = targetResult.Target!;

        bool duplicate;
        if (request.PageId.HasValue)
        {
            int pageId = request.PageId.Value;
            duplicate = await Entries.AnyAsync(
                entry =>
                    entry.Id != currentId &&
                    !entry.IsDeleted &&
                    entry.PageId == pageId,
                ct);
        }
        else
        {
            string storedPath = target.StoredPath.ToLower();
            duplicate = await Entries.AnyAsync(
                entry =>
                    entry.Id != currentId &&
                    !entry.IsDeleted &&
                    entry.PageId == null &&
                    entry.Path.ToLower() == storedPath,
                ct);
        }

        if (duplicate)
        {
            return new SeoResolvedTargetResult(
                null,
                409,
                "SEO settings already exist for this target. Restore or edit that record.");
        }

        if (target.ResolvedPath != "*")
        {
            if (request.PageId.HasValue)
            {
                bool collidesWithStatic = SeoRouteRegistry.StaticSeoTargets.Any(route =>
                    route.Route.Equals(
                        target.ResolvedPath,
                        StringComparison.OrdinalIgnoreCase));
                if (collidesWithStatic)
                {
                    return new SeoResolvedTargetResult(
                        null,
                        409,
                        "This database page conflicts with an existing static public route.");
                }
            }
            else
            {
                List<ContentPage> pages = await _db.ContentPages
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(page => !page.IsDeleted && !page.IsArchived)
                    .ToListAsync(ct);

                bool collidesWithPage = pages.Any(page =>
                    SeoRouteRegistry.TryBuildDatabasePagePath(
                        page.Section,
                        page.Slug,
                        out string pagePath,
                        out _) &&
                    pagePath.Equals(
                        target.ResolvedPath,
                        StringComparison.OrdinalIgnoreCase));

                if (collidesWithPage)
                {
                    return new SeoResolvedTargetResult(
                        null,
                        409,
                        "This static route conflicts with an existing database page URL.");
                }
            }
        }

        if (request.SocialImageMediaId.HasValue)
        {
            Guid mediaId = request.SocialImageMediaId.Value;
            bool active = await _db.SpellIcons
                .AsNoTracking()
                .AnyAsync(
                    media =>
                        media.Id == mediaId &&
                        !media.IsDeleted &&
                        !media.IsArchived,
                    ct);
            if (!active)
            {
                return new SeoResolvedTargetResult(
                    null,
                    409,
                    "Select an active media-library image.");
            }
        }

        return targetResult;
    }

    public async Task<SeoResolvedTarget?> ResolveExistingTargetAsync(
        SeoEntry entry,
        CancellationToken ct)
    {
        if (!entry.PageId.HasValue)
        {
            return new SeoResolvedTarget(
                null,
                entry.Path,
                entry.Path,
                entry.Path == "*" ? "Global defaults" : entry.Path,
                entry.Path == "*" ? "global" : "static");
        }

        int pageId = entry.PageId.Value;
        ContentPage? page = await _db.ContentPages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == pageId, ct);

        if (page is null ||
            !SeoRouteRegistry.TryBuildDatabasePagePath(
                page.Section,
                page.Slug,
                out string path,
                out _))
        {
            return null;
        }

        return new SeoResolvedTarget(
            page.Id,
            string.Empty,
            path,
            page.Title,
            "database");
    }

    internal static string? ResolvePublicPath(
        SeoEntry entry,
        IReadOnlyDictionary<int, ContentPage> pages)
    {
        if (entry.PageId.HasValue)
        {
            if (!pages.TryGetValue(entry.PageId.Value, out ContentPage? page) ||
                !SeoRouteRegistry.TryBuildDatabasePagePath(
                    page.Section,
                    page.Slug,
                    out string pagePath,
                    out _))
            {
                return null;
            }

            if (SeoRouteRegistry.StaticSeoTargets.Any(route =>
                    route.Route.Equals(
                        pagePath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return pagePath;
        }

        if (entry.Path == "*")
        {
            return "*";
        }

        return SeoRouteRegistry.TryResolveStaticTarget(
            entry.Path,
            out SeoRouteDefinition? route,
            out _)
            ? route!.Route
            : null;
    }

    private async Task<SeoResolvedTargetResult> ResolveTargetAsync(
        SeoRequest request,
        CancellationToken ct)
    {
        if (request.PageId.HasValue)
        {
            int pageId = request.PageId.Value;
            ContentPage? page = await _db.ContentPages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == pageId, ct);

            if (page is null || page.IsDeleted || page.IsArchived)
            {
                return new SeoResolvedTargetResult(
                    null,
                    409,
                    "Choose an existing, unarchived database page.");
            }

            if (!SeoRouteRegistry.TryBuildDatabasePagePath(
                    page.Section,
                    page.Slug,
                    out string pagePath,
                    out string? pageError))
            {
                return new SeoResolvedTargetResult(null, 409, pageError);
            }

            return new SeoResolvedTargetResult(
                new SeoResolvedTarget(
                    page.Id,
                    string.Empty,
                    pagePath,
                    page.Title,
                    "database"),
                200,
                null);
        }

        if (!SeoRouteRegistry.TryResolveStaticTarget(
                request.Path,
                out SeoRouteDefinition? route,
                out string? error))
        {
            return new SeoResolvedTargetResult(null, 400, error);
        }

        string path = route!.Route;
        return new SeoResolvedTargetResult(
            new SeoResolvedTarget(
                null,
                path,
                path,
                path == "*" ? "Global defaults" : path,
                path == "*" ? "global" : "static"),
            200,
            null);
    }
}
