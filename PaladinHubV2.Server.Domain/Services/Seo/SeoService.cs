using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed record SeoRequest(
    int? PageId,
    string? Path,
    string? Title,
    string? Description,
    string? CanonicalUrl,
    string? SocialTitle,
    string? SocialDescription,
    Guid? SocialImageMediaId,
    string? ImageUrl,
    bool? Index,
    bool? Follow,
    int Version);

public sealed record SeoResult(
    int Status,
    string? Message = null,
    SeoAdminItem? Entry = null);

public sealed record SeoAdminItem(
    Guid Id,
    int? PageId,
    string Path,
    string ResolvedPath,
    string TargetType,
    string TargetLabel,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    Guid? SocialImageMediaId,
    string ImageUrl,
    bool? Index,
    bool? Follow,
    bool IsArchived,
    bool IsDeleted,
    int Version);

public sealed record SeoHistoryItem(
    Guid Id,
    int Version,
    string Action,
    string Actor,
    DateTime CreatedAtUtc);

public sealed record SeoTargetPage(
    int Id,
    string Title,
    string Path,
    bool IsPublished);

public sealed record SeoPublicEntry(
    Guid Id,
    int Version,
    int? PageId,
    string Path,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    string ImageUrl,
    bool? Index,
    bool? Follow);

public sealed record SeoPublicPage(
    int Id,
    string Title,
    string Path);

public sealed record SeoPublicSnapshot(
    string SiteUrl,
    string RegistryVersion,
    string SnapshotVersion,
    DateTime GeneratedAtUtc,
    IReadOnlyList<string> StaticRoutes,
    IReadOnlyList<SeoPublicPage> Pages,
    IReadOnlyList<SeoPublicEntry> Entries);

internal sealed record SeoSnapshot(
    int? PageId,
    string Path,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    Guid? SocialImageMediaId,
    string ImageUrl,
    bool? Index,
    bool? Follow,
    bool IsArchived,
    bool IsDeleted);

public sealed class SeoService
{
    private const long MutationLockKey = 8820414;

    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;

    public SeoService(AppDbContext db)
    {
        _db = db;
        _assignments = new GameDataAssignmentService(db);
    }

    private DbSet<SeoEntry> Entries => _db.Set<SeoEntry>();
    private DbSet<SeoRevision> Revisions => _db.Set<SeoRevision>();

    public async Task<IReadOnlyList<SeoAdminItem>> ListAsync(CancellationToken ct)
    {
        List<SeoEntry> entries = await Entries
            .AsNoTracking()
            .OrderBy(entry => entry.Path)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);

        int[] pageIds = entries
            .Where(entry => entry.PageId.HasValue)
            .Select(entry => entry.PageId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<int, ContentPage> pages = pageIds.Length == 0
            ? []
            : await _db.ContentPages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(page => pageIds.Contains(page.Id))
                .ToDictionaryAsync(page => page.Id, ct);

        return entries
            .Select(entry => ToAdminItem(entry, pages))
            .ToArray();
    }

    public async Task<IReadOnlyList<SeoTargetPage>> ListPagesAsync(CancellationToken ct)
    {
        List<ContentPage> pages = await _db.ContentPages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(page => !page.IsDeleted && !page.IsArchived)
            .OrderBy(page => page.Section)
            .ThenBy(page => page.Title)
            .ToListAsync(ct);

        List<SeoTargetPage> targets = [];
        foreach (ContentPage page in pages)
        {
            if (!SeoRouteRegistry.TryBuildDatabasePagePath(
                    page.Section,
                    page.Slug,
                    out string path,
                    out _))
            {
                continue;
            }

            targets.Add(new SeoTargetPage(
                page.Id,
                page.Title,
                path,
                page.IsPublished));
        }

        return targets;
    }

    public async Task<IReadOnlyList<SeoHistoryItem>> HistoryAsync(
        Guid id,
        CancellationToken ct)
    {
        return await Revisions
            .AsNoTracking()
            .Where(revision => revision.EntryId == id)
            .OrderByDescending(revision => revision.Version)
            .Select(revision => new SeoHistoryItem(
                revision.Id,
                revision.Version,
                revision.Action,
                revision.Actor,
                revision.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<SeoResult> SaveAsync(
        Guid? id,
        SeoRequest request,
        string actor,
        CancellationToken ct)
    {
        string? shapeError = ValidateShape(request);
        if (shapeError is not null)
        {
            return new SeoResult(400, shapeError);
        }

        await using var transaction = await _assignments.BeginAsync(ct);
        await AcquireMutationLockAsync(ct);

        SeoEntry? entry = id.HasValue
            ? await Entries.SingleOrDefaultAsync(item => item.Id == id.Value, ct)
            : new SeoEntry();

        if (entry is null)
        {
            return new SeoResult(404, "SEO entry not found.");
        }

        if (id.HasValue && entry.Version != request.Version)
        {
            return new SeoResult(409, "Settings changed. Reload before saving.");
        }

        if (entry.IsDeleted || entry.IsArchived)
        {
            return new SeoResult(
                409,
                "Restore or unarchive this SEO entry before editing it.");
        }

        ResolvedTargetResult targetResult = await ResolveAndValidateRelationsAsync(
            entry.Id,
            request,
            ct);
        if (targetResult.Error is not null)
        {
            return new SeoResult(targetResult.Status, targetResult.Error);
        }

        Apply(entry, request, targetResult.Target!);
        if (id.HasValue)
        {
            entry.Version++;
        }
        else
        {
            Entries.Add(entry);
        }

        Record(entry, id.HasValue ? "updated" : "created", actor);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new SeoResult(
            id.HasValue ? 200 : 201,
            Entry: ToAdminItem(entry, targetResult.Target!));
    }

    public async Task<SeoResult> ChangeAsync(
        Guid id,
        int version,
        string? action,
        Guid? revisionId,
        string actor,
        CancellationToken ct)
    {
        await using var transaction = await _assignments.BeginAsync(ct);
        await AcquireMutationLockAsync(ct);

        SeoEntry? entry = await Entries.SingleOrDefaultAsync(
            item => item.Id == id,
            ct);
        if (entry is null)
        {
            return new SeoResult(404, "SEO entry not found.");
        }

        if (entry.Version != version)
        {
            return new SeoResult(
                409,
                "Settings changed. Reload before continuing.");
        }

        string normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        ResolvedTarget? resolvedTarget = null;

        if (normalizedAction == "restore")
        {
            if (!revisionId.HasValue)
            {
                return new SeoResult(400, "Choose a revision to restore.");
            }

            SeoRevision? revision = await Revisions.SingleOrDefaultAsync(
                item => item.EntryId == id && item.Id == revisionId.Value,
                ct);
            if (revision is null)
            {
                return new SeoResult(404, "Revision not found.");
            }

            SeoSnapshot? snapshot = JsonSerializer.Deserialize<SeoSnapshot>(
                revision.Snapshot);
            if (snapshot is null)
            {
                return new SeoResult(409, "The selected revision cannot be read.");
            }

            if (snapshot.IsDeleted)
            {
                return new SeoResult(
                    400,
                    "Choose a revision from before deletion.");
            }

            SeoRequest restoreRequest = FromSnapshot(snapshot, version);
            string? shapeError = ValidateShape(restoreRequest);
            if (shapeError is not null)
            {
                return new SeoResult(409, shapeError);
            }

            ResolvedTargetResult restoreTarget =
                await ResolveAndValidateRelationsAsync(id, restoreRequest, ct);
            if (restoreTarget.Error is not null)
            {
                return new SeoResult(409, restoreTarget.Error);
            }

            resolvedTarget = restoreTarget.Target;
            Apply(entry, restoreRequest, resolvedTarget!);
            entry.IsDeleted = false;
            entry.IsArchived = snapshot.IsArchived;
        }
        else
        {
            if (entry.IsDeleted)
            {
                return new SeoResult(
                    409,
                    "Restore this SEO entry before changing its lifecycle state.");
            }

            switch (normalizedAction)
            {
                case "archive":
                    entry.IsArchived = true;
                    break;

                case "unarchive":
                {
                    SeoRequest current = ToRequest(entry, version);
                    string? shapeError = ValidateShape(current);
                    if (shapeError is not null)
                    {
                        return new SeoResult(409, shapeError);
                    }

                    ResolvedTargetResult unarchiveTarget =
                        await ResolveAndValidateRelationsAsync(id, current, ct);
                    if (unarchiveTarget.Error is not null)
                    {
                        return new SeoResult(409, unarchiveTarget.Error);
                    }

                    resolvedTarget = unarchiveTarget.Target;
                    entry.IsArchived = false;
                    break;
                }

                case "delete":
                    entry.IsArchived = true;
                    entry.IsDeleted = true;
                    break;

                default:
                    return new SeoResult(400, "Unknown SEO lifecycle action.");
            }
        }

        entry.Version++;
        Record(entry, normalizedAction, actor);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        resolvedTarget ??= await ResolveExistingTargetAsync(entry, ct);
        return new SeoResult(200, Entry: ToAdminItem(entry, resolvedTarget));
    }

    public async Task<SeoPublicSnapshot> GetPublicSnapshotAsync(
        string? siteUrl,
        string apiOrigin,
        CancellationToken ct)
    {
        string normalizedSiteUrl = NormalizeOrigin(siteUrl);
        string normalizedApiOrigin = NormalizeOrigin(apiOrigin);

        List<ContentPage> pages = await _db.ContentPages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(page =>
                page.IsPublished &&
                !page.IsDeleted &&
                !page.IsArchived)
            .OrderBy(page => page.Section)
            .ThenBy(page => page.Slug)
            .ToListAsync(ct);

        Dictionary<int, ContentPage> pageMap = pages.ToDictionary(page => page.Id);

        List<SeoEntry> candidates = await Entries
            .AsNoTracking()
            .Where(entry => !entry.IsDeleted && !entry.IsArchived)
            .OrderBy(entry => entry.Path)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);

        Guid[] mediaIds = candidates
            .Where(entry => entry.SocialImageMediaId.HasValue)
            .Select(entry => entry.SocialImageMediaId!.Value)
            .Distinct()
            .ToArray();

        HashSet<Guid> activeMedia = mediaIds.Length == 0
            ? []
            : (await _db.SpellIcons
                    .AsNoTracking()
                    .Where(media =>
                        mediaIds.Contains(media.Id) &&
                        !media.IsDeleted &&
                        !media.IsArchived)
                    .Select(media => media.Id)
                    .ToListAsync(ct))
                .ToHashSet();

        List<SeoPublicEntry> publicEntries = [];
        foreach (SeoEntry entry in candidates)
        {
            string? resolvedPath = ResolvePublicPath(entry, pageMap);
            if (resolvedPath is null)
            {
                continue;
            }

            string imageUrl = entry.ImageUrl;
            if (entry.SocialImageMediaId.HasValue)
            {
                if (!activeMedia.Contains(entry.SocialImageMediaId.Value))
                {
                    continue;
                }

                imageUrl = CombineOriginAndPath(
                    normalizedApiOrigin,
                    $"/api/spell-icons/{entry.SocialImageMediaId.Value}");
            }

            string canonical = entry.CanonicalUrl;
            if (string.IsNullOrWhiteSpace(canonical) && resolvedPath != "*")
            {
                canonical = CombineOriginAndPath(normalizedSiteUrl, resolvedPath);
            }

            publicEntries.Add(new SeoPublicEntry(
                entry.Id,
                entry.Version,
                entry.PageId,
                resolvedPath,
                entry.Title,
                entry.Description,
                canonical,
                entry.SocialTitle,
                entry.SocialDescription,
                imageUrl,
                entry.Index,
                entry.Follow));
        }

        List<SeoPublicPage> publicPages = [];
        foreach (ContentPage page in pages)
        {
            if (!SeoRouteRegistry.TryBuildDatabasePagePath(
                    page.Section,
                    page.Slug,
                    out string path,
                    out _))
            {
                continue;
            }

            if (SeoRouteRegistry.StaticSeoTargets.Any(route =>
                    route.Route.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            publicPages.Add(new SeoPublicPage(page.Id, page.Title, path));
        }

        var resourceRoutes = new List<string>();
        var products = await _db.Products.AsNoTracking().Include(product => product.Images).OrderBy(product => product.Id).ToListAsync(ct);
        foreach (var product in products)
        {
            // IDs are path segments, never executable URL syntax.
            if (!Guid.TryParse(product.Id, out _)) continue;
            string path = "/products/" + product.Id;
            resourceRoutes.Add(path);
            resourceRoutes.Add("/Products/Details/" + product.Id);
            string image = product.Images.OrderBy(image => image.Id == product.ThumbnailImageId ? 0 : 1)
                .ThenBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault() ?? "";
            AddResourceEntry(path, product.Name, product.Description ?? "", image);
        }
        var discussions = await _db.DiscussionPosts.AsNoTracking().OrderBy(post => post.Id).ToListAsync(ct);
        foreach (var post in discussions)
        {
            string path = "/Discussions/Details/" + post.Id;
            resourceRoutes.Add(path);
            AddResourceEntry(path, post.Title, post.Content, "");
        }

        void AddResourceEntry(string path, string title, string description, string image)
        {
            if (publicEntries.Any(entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
            string plain = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(
                description, "<[^>]*>", " ", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)));
            if (plain.Length > 500) plain = plain[..500];
            if (!IsSafeAbsoluteHttpUrl(image)) image = "";
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("resource:" + path))[..16]);
            publicEntries.Add(new SeoPublicEntry(id, 1, null, path, title, plain, "", "", "", image, null, null));
        }

        string[] staticRoutes = SeoRouteRegistry.StaticSeoTargets
            .Select(route => route.Route).Concat(resourceRoutes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        string snapshotVersion = BuildSnapshotVersion(
            publicEntries,
            publicPages,
            staticRoutes, normalizedSiteUrl, apiOrigin);

        return new SeoPublicSnapshot(
            normalizedSiteUrl,
            SeoRouteRegistry.Version,
            snapshotVersion,
            DateTime.UtcNow,
            staticRoutes,
            publicPages,
            publicEntries);
    }

    public static string? ValidateShape(SeoRequest request)
    {
        string title = request.Title ?? string.Empty;
        string description = request.Description ?? string.Empty;
        string socialTitle = request.SocialTitle ?? string.Empty;
        string socialDescription = request.SocialDescription ?? string.Empty;
        string canonical = request.CanonicalUrl ?? string.Empty;
        string imageUrl = request.ImageUrl ?? string.Empty;

        if (title.Length > 200 || socialTitle.Length > 200)
        {
            return "SEO and social titles allow at most 200 characters.";
        }

        if (description.Length > 500 || socialDescription.Length > 500)
        {
            return "SEO and social descriptions allow at most 500 characters.";
        }

        if (request.SocialImageMediaId.HasValue &&
            !string.IsNullOrWhiteSpace(imageUrl))
        {
            return "Choose either a media-library image or an external social image URL, not both.";
        }

        if (!string.IsNullOrWhiteSpace(canonical) &&
            !IsSafeAbsoluteHttpUrl(canonical))
        {
            return "Canonical URL must be an absolute HTTP/HTTPS URL without userinfo, fragments, backslashes or control characters.";
        }

        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            !IsSafeAbsoluteHttpUrl(imageUrl))
        {
            return "External social image must be an absolute HTTP/HTTPS URL without userinfo, fragments, backslashes or control characters.";
        }

        // This URL namespace is reserved for managed assets. Always require a
        // tracked reference, including when the same route is pasted with a host.
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) &&
            imageUri.AbsolutePath.Contains("/api/spell-icons/", StringComparison.OrdinalIgnoreCase))
            return "Use the media library for /api/spell-icons/ images so their dependency can be protected.";

        if (!request.PageId.HasValue)
        {
            if (!SeoRouteRegistry.TryResolveStaticTarget(
                    request.Path,
                    out SeoRouteDefinition? route,
                    out string? routeError))
            {
                return routeError;
            }

            if (route?.Route == "*" && !string.IsNullOrWhiteSpace(canonical))
            {
                return "Global SEO defaults cannot define a canonical URL. Canonical is resolved per page.";
            }
        }

        return null;
    }

    private async Task<ResolvedTargetResult> ResolveAndValidateRelationsAsync(
        Guid currentId,
        SeoRequest request,
        CancellationToken ct)
    {
        ResolvedTargetResult targetResult = await ResolveTargetAsync(request, ct);
        if (targetResult.Error is not null)
        {
            return targetResult;
        }

        ResolvedTarget target = targetResult.Target!;

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
            return new ResolvedTargetResult(
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
                    return new ResolvedTargetResult(
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
                    return new ResolvedTargetResult(
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
                return new ResolvedTargetResult(
                    null,
                    409,
                    "Select an active media-library image.");
            }
        }

        return targetResult;
    }

    private async Task<ResolvedTargetResult> ResolveTargetAsync(
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
                return new ResolvedTargetResult(
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
                return new ResolvedTargetResult(null, 409, pageError);
            }

            return new ResolvedTargetResult(
                new ResolvedTarget(
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
            return new ResolvedTargetResult(null, 400, error);
        }

        string path = route!.Route;
        return new ResolvedTargetResult(
            new ResolvedTarget(
                null,
                path,
                path,
                path == "*" ? "Global defaults" : path,
                path == "*" ? "global" : "static"),
            200,
            null);
    }

    private async Task<ResolvedTarget?> ResolveExistingTargetAsync(
        SeoEntry entry,
        CancellationToken ct)
    {
        if (!entry.PageId.HasValue)
        {
            return new ResolvedTarget(
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

        return new ResolvedTarget(
            page.Id,
            string.Empty,
            path,
            page.Title,
            "database");
    }

    private static string? ResolvePublicPath(
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

    private static void Apply(
        SeoEntry entry,
        SeoRequest request,
        ResolvedTarget target)
    {
        entry.PageId = target.PageId;
        entry.Path = target.StoredPath;
        entry.Title = (request.Title ?? string.Empty).Trim();
        entry.Description = (request.Description ?? string.Empty).Trim();
        entry.CanonicalUrl = (request.CanonicalUrl ?? string.Empty).Trim();
        entry.SocialTitle = (request.SocialTitle ?? string.Empty).Trim();
        entry.SocialDescription = (request.SocialDescription ?? string.Empty).Trim();
        entry.SocialImageMediaId = request.SocialImageMediaId;
        entry.ImageUrl = (request.ImageUrl ?? string.Empty).Trim();
        entry.Index = request.Index;
        entry.Follow = request.Follow;
    }

    private void Record(SeoEntry entry, string action, string actor)
    {
        string normalizedActor = string.IsNullOrWhiteSpace(actor)
            ? "admin"
            : actor.Trim();
        if (normalizedActor.Length > 256)
        {
            normalizedActor = normalizedActor[..256];
        }

        Revisions.Add(new SeoRevision
        {
            EntryId = entry.Id,
            Version = entry.Version,
            Action = action.Length > 30 ? action[..30] : action,
            Actor = normalizedActor,
            Snapshot = JsonSerializer.Serialize(ToSnapshot(entry)),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task AcquireMutationLockAsync(CancellationToken ct)
    {
        string? provider = _db.Database.ProviderName;
        if (provider?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_xact_lock({MutationLockKey})",
                ct);
        }
    }

    private static bool IsSafeAbsoluteHttpUrl(string value)
    {
        return value.Length <= 2048 &&
               !value.Any(char.IsControl) &&
               !value.Contains('\\') &&
               Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme is "http" or "https" &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    private static string NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string CombineOriginAndPath(string origin, string path)
    {
        return string.IsNullOrWhiteSpace(origin)
            ? string.Empty
            : origin.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }

    private static string BuildSnapshotVersion(
        IEnumerable<SeoPublicEntry> entries,
        IEnumerable<SeoPublicPage> pages,
        IEnumerable<string> staticRoutes, string siteUrl, string apiOrigin)
    {
        string payload = JsonSerializer.Serialize(new {
            SiteUrl = siteUrl, ApiOrigin = apiOrigin, Registry = SeoRouteRegistry.Version,
            Entries = entries.OrderBy(entry => entry.Id),
            Pages = pages.OrderBy(page => page.Id),
            Routes = staticRoutes.OrderBy(route => route, StringComparer.Ordinal)
        });
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

    private static SeoSnapshot ToSnapshot(SeoEntry entry) =>
        new(
            entry.PageId,
            entry.Path,
            entry.Title,
            entry.Description,
            entry.CanonicalUrl,
            entry.SocialTitle,
            entry.SocialDescription,
            entry.SocialImageMediaId,
            entry.ImageUrl,
            entry.Index,
            entry.Follow,
            entry.IsArchived,
            entry.IsDeleted);

    private static SeoRequest FromSnapshot(SeoSnapshot snapshot, int version) =>
        new(
            snapshot.PageId,
            snapshot.Path,
            snapshot.Title,
            snapshot.Description,
            snapshot.CanonicalUrl,
            snapshot.SocialTitle,
            snapshot.SocialDescription,
            snapshot.SocialImageMediaId,
            snapshot.ImageUrl,
            snapshot.Index,
            snapshot.Follow,
            version);

    private static SeoRequest ToRequest(SeoEntry entry, int version) =>
        FromSnapshot(ToSnapshot(entry), version);

    private static SeoAdminItem ToAdminItem(
        SeoEntry entry,
        IReadOnlyDictionary<int, ContentPage> pages)
    {
        ResolvedTarget? target = null;
        if (entry.PageId.HasValue &&
            pages.TryGetValue(entry.PageId.Value, out ContentPage? page) &&
            SeoRouteRegistry.TryBuildDatabasePagePath(
                page.Section,
                page.Slug,
                out string pagePath,
                out _))
        {
            target = new ResolvedTarget(
                page.Id,
                string.Empty,
                pagePath,
                page.Title,
                "database");
        }

        target ??= new ResolvedTarget(
            entry.PageId,
            entry.Path,
            entry.Path,
            entry.PageId.HasValue
                ? "Unavailable target"
                : entry.Path == "*"
                    ? "Global defaults"
                    : entry.Path,
            entry.PageId.HasValue
                ? "database"
                : entry.Path == "*"
                    ? "global"
                    : "static");

        return ToAdminItem(entry, target);
    }

    private static SeoAdminItem ToAdminItem(
        SeoEntry entry,
        ResolvedTarget? target)
    {
        target ??= new ResolvedTarget(
            entry.PageId,
            entry.Path,
            entry.Path,
            "Unavailable target",
            entry.PageId.HasValue ? "database" : "static");

        return new SeoAdminItem(
            entry.Id,
            entry.PageId,
            entry.Path,
            target.ResolvedPath,
            target.TargetType,
            target.Label,
            entry.Title,
            entry.Description,
            entry.CanonicalUrl,
            entry.SocialTitle,
            entry.SocialDescription,
            entry.SocialImageMediaId,
            entry.ImageUrl,
            entry.Index,
            entry.Follow,
            entry.IsArchived,
            entry.IsDeleted,
            entry.Version);
    }

    private sealed record ResolvedTarget(
        int? PageId,
        string StoredPath,
        string ResolvedPath,
        string Label,
        string TargetType);

    private sealed record ResolvedTargetResult(
        ResolvedTarget? Target,
        int Status,
        string? Error);
}
