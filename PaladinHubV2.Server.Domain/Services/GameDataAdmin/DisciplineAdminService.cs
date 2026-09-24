using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DisciplineAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly IDisciplineAdminQueryService _queries;
    private readonly IDisciplineAdminValidator _validator;
    private readonly IDisciplineUsageGuard _usage;
    private readonly IDisciplineRevisionJournal _journal;

    internal DisciplineAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new DisciplineAdminQueryService(db),
            new DisciplineAdminValidator(db),
            new DisciplineUsageGuard(db),
            new DisciplineRevisionJournal(db))
    {
    }

    public DisciplineAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        IDisciplineAdminQueryService queries,
        IDisciplineAdminValidator validator,
        IDisciplineUsageGuard usage,
        IDisciplineRevisionJournal journal)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
    }

    public Task<List<DisciplineListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<List<DisciplineRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(id, cancellationToken);

    public async Task<DisciplineAdminResult> CreateAsync(
        DisciplineRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        var discipline = new GameDiscipline();

        string? error = await _validator.ValidateAsync(
            discipline.Id,
            request,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(discipline, request);

        _db.GameDisciplines.Add(discipline);
        await _db.SaveChangesAsync(cancellationToken);

        _journal.Record(discipline, "created", actor);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Success(discipline);
    }

    public async Task<DisciplineAdminResult> UpdateAsync(
        int id,
        DisciplineRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        GameDiscipline? discipline =
            await _db.GameDisciplines.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (discipline is null)
        {
            return Error(DisciplineAdminError.NotFound);
        }

        if (request.Version != discipline.Version)
        {
            return Error(DisciplineAdminError.Stale);
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
            discipline.IsArchived,
            request.IsArchived);

        Apply(discipline, request);
        discipline.Version++;

        _journal.Record(discipline, action, actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(discipline);
    }

    public async Task<DisciplineAdminResult> DeleteAsync(
        int id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        GameDiscipline? discipline =
            await _db.GameDisciplines.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (discipline is null)
        {
            return Error(DisciplineAdminError.NotFound);
        }

        if (version != discipline.Version)
        {
            return Error(DisciplineAdminError.Stale);
        }

        if (await _usage.IsInUseAsync(
                id,
                cancellationToken))
        {
            return new DisciplineAdminResult(
                DisciplineAdminError.InUse,
                Message:
                    "Move specializations and assigned records before deleting this entry, or archive it.");
        }

        discipline.IsDeleted = true;
        discipline.Version++;

        _journal.Record(discipline, "deleted", actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Error(DisciplineAdminError.None);
    }

    public async Task<DisciplineAdminResult> RestoreAsync(
        int id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        GameDiscipline? discipline =
            await _db.GameDisciplines.SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (discipline is null)
        {
            return Error(DisciplineAdminError.NotFound);
        }

        if (request.Version != discipline.Version)
        {
            return Error(DisciplineAdminError.Stale);
        }

        DisciplineRevision? revision =
            await _db.DisciplineRevisions.SingleOrDefaultAsync(
                item =>
                    item.Id == request.RevisionId &&
                    item.DisciplineId == id,
                cancellationToken);

        if (revision is null)
        {
            return Error(
                DisciplineAdminError.RevisionNotFound);
        }

        GameDiscipline snapshot =
            _journal.ReadSnapshot(revision);

        if (snapshot.IsDeleted)
        {
            return new DisciplineAdminResult(
                DisciplineAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        DisciplineRequest restored =
            BuildRestoreRequest(
                snapshot,
                discipline.Version);

        string? error = await _validator.ValidateAsync(
            id,
            restored,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(discipline, restored);
        discipline.IsDeleted = false;
        discipline.Version++;

        _journal.Record(discipline, "restored", actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(discipline);
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

    internal static DisciplineRequest BuildRestoreRequest(
        GameDiscipline snapshot,
        int currentVersion)
    {
        return new DisciplineRequest(
            snapshot.Name,
            snapshot.Description,
            snapshot.ParentId,
            snapshot.SortOrder,
            snapshot.IsArchived,
            currentVersion);
    }

    internal static void Apply(
        GameDiscipline discipline,
        DisciplineRequest request)
    {
        discipline.Name = request.Name.Trim();
        discipline.Description =
            request.Description?.Trim() ??
            string.Empty;
        discipline.ParentId = request.ParentId;
        discipline.SortOrder = request.SortOrder;
        discipline.IsArchived = request.IsArchived;
    }

    private static DisciplineAdminResult Success(
        GameDiscipline discipline) =>
        new(
            DisciplineAdminError.None,
            discipline);

    private static DisciplineAdminResult Validation(
        string message) =>
        new(
            DisciplineAdminError.Validation,
            Message: message);

    private static DisciplineAdminResult Error(
        DisciplineAdminError error) =>
        new(error);
}
