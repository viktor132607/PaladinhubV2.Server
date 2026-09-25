using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly IMediaAdminQueryService _queries;
    private readonly IMediaAdminValidator _validator;
    private readonly IMediaUsageCounter _usage;
    private readonly IMediaRevisionJournal _journal;

    public MediaAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new MediaAdminQueryService(
                db,
                new MediaUsageCounter(
                    db,
                    new MediaBannerUsageLookup(db))),
            new MediaAdminValidator(),
            new MediaUsageCounter(
                db,
                new MediaBannerUsageLookup(db)),
            new MediaRevisionJournal(db))
    {
    }

    public MediaAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        IMediaAdminQueryService queries,
        IMediaAdminValidator validator,
        IMediaUsageCounter usage,
        IMediaRevisionJournal journal)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
    }

    public Task<MediaPageResult> ListAsync(
        string? search,
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        _queries.ListAsync(
            search,
            status,
            page,
            pageSize,
            cancellationToken);

    public Task<List<MediaRevision>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(
            id,
            cancellationToken);

    public async Task<MediaAdminResult> UpdateAsync(
        Guid id,
        MediaRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        string? validation =
            _validator.Validate(request);

        if (validation is not null)
        {
            return Validation(validation);
        }

        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        SpellIcon? media =
            await _db.SpellIcons
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (media is null)
        {
            return Error(
                MediaAdminError.NotFound);
        }

        if (media.Version != request.Version)
        {
            return Error(
                MediaAdminError.Stale);
        }

        if (ShouldCheckUsageBeforeArchive(
                media.IsArchived,
                request.IsArchived) &&
            await _usage.CountAsync(
                id,
                cancellationToken) > 0)
        {
            return new MediaAdminResult(
                MediaAdminError.InUse,
                Message:
                    "This image is in use. Remove its references before archiving it.");
        }

        string action =
            ResolveUpdateAction(
                media.IsArchived,
                request.IsArchived);

        Apply(
            media,
            request);

        media.Version++;

        _journal.Record(
            media,
            action,
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(media.Id);
    }

    public async Task<MediaAdminResult> DeleteAsync(
        Guid id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        SpellIcon? media =
            await _db.SpellIcons
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (media is null)
        {
            return Error(
                MediaAdminError.NotFound);
        }

        if (media.Version != version)
        {
            return Error(
                MediaAdminError.Stale);
        }

        if (await _usage.CountAsync(
                id,
                cancellationToken) > 0)
        {
            return new MediaAdminResult(
                MediaAdminError.InUse,
                Message:
                    "This image is in use. Remove its references or archive it.");
        }

        media.IsDeleted = true;
        media.Version++;

        _journal.Record(
            media,
            "deleted",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Error(
            MediaAdminError.None);
    }

    public async Task<MediaAdminResult> RestoreAsync(
        Guid id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        SpellIcon? media =
            await _db.SpellIcons
                .SingleOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);

        if (media is null)
        {
            return Error(
                MediaAdminError.NotFound);
        }

        if (media.Version != request.Version)
        {
            return Error(
                MediaAdminError.Stale);
        }

        MediaRevision? revision =
            await _db.MediaRevisions
                .SingleOrDefaultAsync(
                    item =>
                        item.MediaId == id &&
                        item.Id ==
                        request.RevisionId,
                    cancellationToken);

        if (revision is null)
        {
            return Error(
                MediaAdminError.RevisionNotFound);
        }

        MediaSnapshot? snapshot =
            _journal.ReadSnapshot(
                revision);

        if (snapshot is null)
        {
            return Error(
                MediaAdminError.RevisionNotFound);
        }

        if (snapshot.IsDeleted)
        {
            return new MediaAdminResult(
                MediaAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        if (snapshot.IsArchived &&
            await _usage.CountAsync(
                id,
                cancellationToken) > 0)
        {
            return new MediaAdminResult(
                MediaAdminError.InUse,
                Message:
                    "This image is in use and cannot be restored to an archived state.");
        }

        ApplySnapshot(
            media,
            snapshot);

        media.IsDeleted = false;
        media.Version++;

        _journal.Record(
            media,
            "restored",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(media.Id);
    }

    internal static bool ShouldCheckUsageBeforeArchive(
        bool wasArchived,
        bool isArchived) =>
        !wasArchived && isArchived;

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

    internal static void Apply(
        SpellIcon media,
        MediaRequest request)
    {
        media.Name =
            request.Name.Trim();

        media.AltText =
            request.AltText?.Trim() ??
            string.Empty;

        media.Description =
            request.Description?.Trim() ??
            string.Empty;

        media.IsArchived =
            request.IsArchived;
    }

    internal static void ApplySnapshot(
        SpellIcon media,
        MediaSnapshot snapshot)
    {
        media.Name = snapshot.Name;
        media.AltText = snapshot.AltText;
        media.Description =
            snapshot.Description;
        media.IsArchived =
            snapshot.IsArchived;
    }

    private static MediaAdminResult Success(
        Guid id) =>
        new(
            MediaAdminError.None,
            id);

    private static MediaAdminResult Validation(
        string message) =>
        new(
            MediaAdminError.Validation,
            Message: message);

    private static MediaAdminResult Error(
        MediaAdminError error) =>
        new(error);
}
