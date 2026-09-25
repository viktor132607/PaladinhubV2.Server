using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class PatchAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly IPatchAdminQueryService _queries;
    private readonly IPatchAdminValidator _validator;
    private readonly IPatchUsageGuard _usage;
    private readonly IPatchRevisionJournal _journal;

    public PatchAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new PatchAdminQueryService(db),
            new PatchAdminValidator(db),
            new PatchUsageGuard(db),
            new PatchRevisionJournal(db))
    {
    }

    public PatchAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        IPatchAdminQueryService queries,
        IPatchAdminValidator validator,
        IPatchUsageGuard usage,
        IPatchRevisionJournal journal)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
    }

    public Task<List<PatchListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<List<PatchRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(
            id,
            cancellationToken);

    public async Task<PatchAdminResult> CreateAsync(
        PatchRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        var patch = new GamePatch();

        string? error =
            await _validator.ValidateAsync(
                patch.Id,
                request,
                cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(
            patch,
            request);

        _db.GamePatches.Add(patch);

        await _db.SaveChangesAsync(
            cancellationToken);

        _journal.Record(
            patch,
            "created",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(patch);
    }

    public async Task<PatchAdminResult> UpdateAsync(
        int id,
        PatchRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GamePatch? patch =
            await _db.GamePatches
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (patch is null)
        {
            return Error(
                PatchAdminError.NotFound);
        }

        if (request.Version != patch.Version)
        {
            return Error(
                PatchAdminError.Stale);
        }

        string? error =
            await _validator.ValidateAsync(
                id,
                request,
                cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        string action =
            ResolveUpdateAction(
                patch.IsArchived,
                request.IsArchived);

        Apply(
            patch,
            request);

        patch.Version++;

        _journal.Record(
            patch,
            action,
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(patch);
    }

    public async Task<PatchAdminResult> DeleteAsync(
        int id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GamePatch? patch =
            await _db.GamePatches
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (patch is null)
        {
            return Error(
                PatchAdminError.NotFound);
        }

        if (version != patch.Version)
        {
            return Error(
                PatchAdminError.Stale);
        }

        if (await _usage.IsInUseAsync(
                id,
                cancellationToken))
        {
            return new PatchAdminResult(
                PatchAdminError.InUse,
                Message:
                    "Remove this patch from assigned records before deleting it, or archive it.");
        }

        patch.IsDeleted = true;
        patch.Version++;

        _journal.Record(
            patch,
            "deleted",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Error(
            PatchAdminError.None);
    }

    public async Task<PatchAdminResult> RestoreAsync(
        int id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GamePatch? patch =
            await _db.GamePatches
                .SingleOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);

        if (patch is null)
        {
            return Error(
                PatchAdminError.NotFound);
        }

        if (request.Version != patch.Version)
        {
            return Error(
                PatchAdminError.Stale);
        }

        PatchRevision? revision =
            await _db.PatchRevisions
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            request.RevisionId &&
                        item.PatchId == id,
                    cancellationToken);

        if (revision is null)
        {
            return Error(
                PatchAdminError.RevisionNotFound);
        }

        GamePatch snapshot =
            _journal.ReadSnapshot(revision);

        if (snapshot.IsDeleted)
        {
            return new PatchAdminResult(
                PatchAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        PatchRequest restored =
            BuildRestoreRequest(
                snapshot,
                patch.Version);

        string? error =
            await _validator.ValidateAsync(
                id,
                restored,
                cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(
            patch,
            restored);

        patch.IsDeleted = false;
        patch.Version++;

        _journal.Record(
            patch,
            "restored",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(patch);
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

    internal static PatchRequest BuildRestoreRequest(
        GamePatch snapshot,
        int currentVersion)
    {
        return new PatchRequest(
            snapshot.Name,
            snapshot.Description,
            snapshot.SortOrder,
            snapshot.IsArchived,
            currentVersion);
    }

    internal static void Apply(
        GamePatch patch,
        PatchRequest request)
    {
        patch.Name =
            request.Name.Trim();

        patch.Description =
            request.Description?.Trim() ??
            string.Empty;

        patch.SortOrder =
            request.SortOrder;

        patch.IsArchived =
            request.IsArchived;
    }

    private static PatchAdminResult Success(
        GamePatch patch) =>
        new(
            PatchAdminError.None,
            patch);

    private static PatchAdminResult Validation(
        string message) =>
        new(
            PatchAdminError.Validation,
            Message: message);

    private static PatchAdminResult Error(
        PatchAdminError error) =>
        new(error);
}
