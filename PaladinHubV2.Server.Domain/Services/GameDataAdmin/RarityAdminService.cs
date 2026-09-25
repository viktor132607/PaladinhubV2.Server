using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly IRarityAdminQueryService _queries;
    private readonly IRarityAdminValidator _validator;
    private readonly IRarityUsageGuard _usage;
    private readonly IRarityRevisionJournal _journal;
    private readonly IRarityQualitySynchronizer _quality;

    public RarityAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new RarityAdminQueryService(db),
            new RarityAdminValidator(db),
            new RarityUsageGuard(db),
            new RarityRevisionJournal(db),
            new RarityQualitySynchronizer(db))
    {
    }

    public RarityAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        IRarityAdminQueryService queries,
        IRarityAdminValidator validator,
        IRarityUsageGuard usage,
        IRarityRevisionJournal journal,
        IRarityQualitySynchronizer quality)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
        _quality = quality;
    }

    public Task<List<RarityListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<List<RarityRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(id, cancellationToken);

    public async Task<RarityAdminResult> CreateAsync(
        RarityRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        var rarity = new ItemRarity();

        string? error = await _validator.ValidateAsync(
            rarity.Id,
            request,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(rarity, request);
        _db.ItemRarities.Add(rarity);
        await _db.SaveChangesAsync(cancellationToken);

        _journal.Record(rarity, "created", actor);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(rarity);
    }

    public async Task<RarityAdminResult> UpdateAsync(
        int id,
        RarityRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        ItemRarity? rarity =
            await _db.ItemRarities.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (rarity is null)
        {
            return Error(RarityAdminError.NotFound);
        }

        if (request.Version != rarity.Version)
        {
            return Error(RarityAdminError.Stale);
        }

        string? error = await _validator.ValidateAsync(
            id,
            request,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        string action = ResolveUpdateAction(
            rarity.IsArchived,
            request.IsArchived);

        Apply(rarity, request);
        rarity.Version++;

        await _quality.SyncAsync(
            rarity,
            cancellationToken);

        _journal.Record(
            rarity,
            action,
            actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(rarity);
    }

    public async Task<RarityAdminResult> DeleteAsync(
        int id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        ItemRarity? rarity =
            await _db.ItemRarities.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (rarity is null)
        {
            return Error(RarityAdminError.NotFound);
        }

        if (version != rarity.Version)
        {
            return Error(RarityAdminError.Stale);
        }

        if (await _usage.IsInUseAsync(
                id,
                cancellationToken))
        {
            return new RarityAdminResult(
                RarityAdminError.InUse,
                Message:
                    "Remove this rarity from assigned records before deleting it, or archive it.");
        }

        rarity.IsDeleted = true;
        rarity.Version++;

        _journal.Record(
            rarity,
            "deleted",
            actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Error(RarityAdminError.None);
    }

    public async Task<RarityAdminResult> RestoreAsync(
        int id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        ItemRarity? rarity =
            await _db.ItemRarities.SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (rarity is null)
        {
            return Error(RarityAdminError.NotFound);
        }

        if (request.Version != rarity.Version)
        {
            return Error(RarityAdminError.Stale);
        }

        RarityRevision? revision =
            await _db.RarityRevisions.SingleOrDefaultAsync(
                item =>
                    item.Id == request.RevisionId &&
                    item.RarityId == id,
                cancellationToken);

        if (revision is null)
        {
            return Error(
                RarityAdminError.RevisionNotFound);
        }

        ItemRarity snapshot =
            _journal.ReadSnapshot(revision);

        if (snapshot.IsDeleted)
        {
            return new RarityAdminResult(
                RarityAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        RarityRequest restored =
            BuildRestoreRequest(
                snapshot,
                rarity.Version);

        string? error = await _validator.ValidateAsync(
            id,
            restored,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(rarity, restored);
        rarity.IsDeleted = false;
        rarity.Version++;

        await _quality.SyncAsync(
            rarity,
            cancellationToken);

        _journal.Record(
            rarity,
            "restored",
            actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(rarity);
    }

    internal static string ResolveUpdateAction(
        bool wasArchived,
        bool isArchived)
    {
        if (wasArchived == isArchived)
        {
            return "updated";
        }

        return isArchived
            ? "archived"
            : "unarchived";
    }

    internal static RarityRequest BuildRestoreRequest(
        ItemRarity snapshot,
        int currentVersion)
    {
        return new RarityRequest(
            snapshot.Name,
            snapshot.Description,
            snapshot.Color,
            snapshot.SortOrder,
            snapshot.IsArchived,
            currentVersion);
    }

    internal static void Apply(
        ItemRarity rarity,
        RarityRequest request)
    {
        rarity.Name = request.Name.Trim();
        rarity.Description =
            request.Description?.Trim() ??
            string.Empty;
        rarity.Color = request.Color;
        rarity.SortOrder = request.SortOrder;
        rarity.IsArchived = request.IsArchived;
    }

    private static RarityAdminResult Success(
        ItemRarity rarity) =>
        new(
            RarityAdminError.None,
            rarity);

    private static RarityAdminResult Validation(
        string message) =>
        new(
            RarityAdminError.Validation,
            Message: message);

    private static RarityAdminResult Error(
        RarityAdminError error) =>
        new(error);
}
