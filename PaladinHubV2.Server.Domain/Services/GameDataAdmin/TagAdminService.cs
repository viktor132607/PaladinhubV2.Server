using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class TagAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly ITagAdminQueryService _queries;
    private readonly ITagAdminValidator _validator;
    private readonly ITagUsageGuard _usage;
    private readonly ITagRevisionJournal _journal;

    public TagAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new TagAdminQueryService(db),
            new TagAdminValidator(db),
            new TagUsageGuard(db),
            new TagRevisionJournal(db))
    {
    }

    public TagAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        ITagAdminQueryService queries,
        ITagAdminValidator validator,
        ITagUsageGuard usage,
        ITagRevisionJournal journal)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
    }

    public Task<List<TagListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<List<TagRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(
            id,
            cancellationToken);

    public async Task<TagAdminResult> CreateAsync(
        TagRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        var tag = new GameTag();

        string? error =
            await _validator.ValidateAsync(
                tag.Id,
                request,
                cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(
            tag,
            request);

        _db.GameTags.Add(tag);

        await _db.SaveChangesAsync(
            cancellationToken);

        _journal.Record(
            tag,
            "created",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(tag);
    }

    public async Task<TagAdminResult> UpdateAsync(
        int id,
        TagRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GameTag? tag =
            await _db.GameTags
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (tag is null)
        {
            return Error(
                TagAdminError.NotFound);
        }

        if (request.Version != tag.Version)
        {
            return Error(
                TagAdminError.Stale);
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
                tag.IsArchived,
                request.IsArchived);

        Apply(
            tag,
            request);

        tag.Version++;

        _journal.Record(
            tag,
            action,
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(tag);
    }

    public async Task<TagAdminResult> DeleteAsync(
        int id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GameTag? tag =
            await _db.GameTags
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    cancellationToken);

        if (tag is null)
        {
            return Error(
                TagAdminError.NotFound);
        }

        if (version != tag.Version)
        {
            return Error(
                TagAdminError.Stale);
        }

        if (await _usage.IsInUseAsync(
                id,
                cancellationToken))
        {
            return new TagAdminResult(
                TagAdminError.InUse,
                Message:
                    "Remove this tag from assigned records before deleting it, or archive it.");
        }

        tag.IsDeleted = true;
        tag.Version++;

        _journal.Record(
            tag,
            "deleted",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Error(
            TagAdminError.None);
    }

    public async Task<TagAdminResult> RestoreAsync(
        int id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(
                cancellationToken);

        GameTag? tag =
            await _db.GameTags
                .SingleOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);

        if (tag is null)
        {
            return Error(
                TagAdminError.NotFound);
        }

        if (request.Version != tag.Version)
        {
            return Error(
                TagAdminError.Stale);
        }

        TagRevision? revision =
            await _db.TagRevisions
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            request.RevisionId &&
                        item.TagId == id,
                    cancellationToken);

        if (revision is null)
        {
            return Error(
                TagAdminError.RevisionNotFound);
        }

        GameTag snapshot =
            _journal.ReadSnapshot(revision);

        if (snapshot.IsDeleted)
        {
            return new TagAdminResult(
                TagAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        TagRequest restored =
            BuildRestoreRequest(
                snapshot,
                tag.Version);

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
            tag,
            restored);

        tag.IsDeleted = false;
        tag.Version++;

        _journal.Record(
            tag,
            "restored",
            actor);

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Success(tag);
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

    internal static TagRequest BuildRestoreRequest(
        GameTag snapshot,
        int currentVersion)
    {
        return new TagRequest(
            snapshot.Name,
            snapshot.Description,
            snapshot.SortOrder,
            snapshot.IsArchived,
            currentVersion);
    }

    internal static void Apply(
        GameTag tag,
        TagRequest request)
    {
        tag.Name =
            request.Name.Trim();

        tag.Description =
            request.Description?.Trim() ??
            string.Empty;

        tag.SortOrder =
            request.SortOrder;

        tag.IsArchived =
            request.IsArchived;
    }

    private static TagAdminResult Success(
        GameTag tag) =>
        new(
            TagAdminError.None,
            tag);

    private static TagAdminResult Validation(
        string message) =>
        new(
            TagAdminError.Validation,
            Message: message);

    private static TagAdminResult Error(
        TagAdminError error) =>
        new(error);
}
