using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed class SeoService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly ISeoTargetResolver _targets;
    private readonly ISeoPublicSnapshotService _publicSnapshots;
    private readonly ISeoMutationLock _mutationLock;

    public SeoService(AppDbContext db)
        : this(
            db,
            new GameDataAssignmentService(db),
            new SeoTargetResolver(db),
            new SeoPublicSnapshotService(db),
            new SeoMutationLock(db))
    {
    }

    public SeoService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        ISeoTargetResolver targets,
        ISeoPublicSnapshotService publicSnapshots,
        ISeoMutationLock mutationLock)
    {
        _db = db;
        _assignments = assignments;
        _targets = targets;
        _publicSnapshots = publicSnapshots;
        _mutationLock = mutationLock;
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
            .Select(entry => SeoEntryMapper.ToAdminItem(entry, pages))
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
        string? shapeError = SeoRequestValidator.Validate(request);
        if (shapeError is not null)
        {
            return new SeoResult(400, shapeError);
        }

        await using var transaction = await _assignments.BeginAsync(ct);
        await _mutationLock.AcquireAsync(ct);

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

        SeoResolvedTargetResult targetResult =
            await _targets.ResolveAndValidateRelationsAsync(
                entry.Id,
                request,
                ct);
        if (targetResult.Error is not null)
        {
            return new SeoResult(targetResult.Status, targetResult.Error);
        }

        SeoEntryMapper.Apply(entry, request, targetResult.Target!);
        if (id.HasValue)
        {
            entry.Version++;
        }
        else
        {
            Entries.Add(entry);
        }

        Revisions.Add(SeoRevisionFactory.Create(
            entry,
            id.HasValue ? "updated" : "created",
            actor));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new SeoResult(
            id.HasValue ? 200 : 201,
            Entry: SeoEntryMapper.ToAdminItem(entry, targetResult.Target!));
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
        await _mutationLock.AcquireAsync(ct);

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

        string normalizedAction =
            action?.Trim().ToLowerInvariant() ?? string.Empty;
        SeoResolvedTarget? resolvedTarget = null;

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
                return new SeoResult(
                    409,
                    "The selected revision cannot be read.");
            }

            if (snapshot.IsDeleted)
            {
                return new SeoResult(
                    400,
                    "Choose a revision from before deletion.");
            }

            SeoRequest restoreRequest =
                SeoEntryMapper.FromSnapshot(snapshot, version);
            string? shapeError = SeoRequestValidator.Validate(restoreRequest);
            if (shapeError is not null)
            {
                return new SeoResult(409, shapeError);
            }

            SeoResolvedTargetResult restoreTarget =
                await _targets.ResolveAndValidateRelationsAsync(
                    id,
                    restoreRequest,
                    ct);
            if (restoreTarget.Error is not null)
            {
                return new SeoResult(409, restoreTarget.Error);
            }

            resolvedTarget = restoreTarget.Target;
            SeoEntryMapper.Apply(entry, restoreRequest, resolvedTarget!);
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
                    SeoRequest current =
                        SeoEntryMapper.ToRequest(entry, version);
                    string? shapeError = SeoRequestValidator.Validate(current);
                    if (shapeError is not null)
                    {
                        return new SeoResult(409, shapeError);
                    }

                    SeoResolvedTargetResult unarchiveTarget =
                        await _targets.ResolveAndValidateRelationsAsync(
                            id,
                            current,
                            ct);
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
                    return new SeoResult(
                        400,
                        "Unknown SEO lifecycle action.");
            }
        }

        entry.Version++;
        Revisions.Add(SeoRevisionFactory.Create(
            entry,
            normalizedAction,
            actor));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        resolvedTarget ??=
            await _targets.ResolveExistingTargetAsync(entry, ct);

        return new SeoResult(
            200,
            Entry: SeoEntryMapper.ToAdminItem(entry, resolvedTarget));
    }

    public Task<SeoPublicSnapshot> GetPublicSnapshotAsync(
        string? siteUrl,
        string apiOrigin,
        CancellationToken ct)
    {
        return _publicSnapshots.BuildAsync(siteUrl, apiOrigin, ct);
    }

    public static string? ValidateShape(SeoRequest request)
    {
        return SeoRequestValidator.Validate(request);
    }
}
